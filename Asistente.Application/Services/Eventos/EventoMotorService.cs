using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Eventos;

/// <summary>
/// Motor de Eventos Empresariales (ETAPA 13 - Actividades 1, 4).
///
/// Responsabilidades:
///  - Detectar eventos (por código).
///  - Evaluar reglas activas (por prioridad) y su condición.
///  - Ejecutar el workflow asociado de forma automática (vía WorkflowEngine, reutilizando
///    autorización y auditoría de herramientas).
///  - Registrar auditoría completa en EventosProcesados (Actividad 9).
///  - Aplicar políticas de reintento configuradas (Actividad 6).
///
/// El motor es desacoplado: un evento dispara la evaluación y la ejecución queda registrada
/// para ser procesada (con reintentos) por el BackgroundService ProcesadorEventosBackgroundService.
/// </summary>
public class EventoMotorService : IEventoMotorService
{
    private readonly IEventoEmpresarialRepository _eventoRepository;
    private readonly IReglaEventoRepository _reglaRepository;
    private readonly IEventoProcesadoRepository _eventoProcesadoRepository;
    private readonly IConfiguracionEventoMotorRepository _configRepository;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EventoMotorService> _logger;

    public EventoMotorService(
        IEventoEmpresarialRepository eventoRepository,
        IReglaEventoRepository reglaRepository,
        IEventoProcesadoRepository eventoProcesadoRepository,
        IConfiguracionEventoMotorRepository configRepository,
        IWorkflowEngine workflowEngine,
        IUnitOfWork unitOfWork,
        ILogger<EventoMotorService> logger)
    {
        _eventoRepository = eventoRepository;
        _reglaRepository = reglaRepository;
        _eventoProcesadoRepository = eventoProcesadoRepository;
        _configRepository = configRepository;
        _workflowEngine = workflowEngine;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Dispara un evento por su código. Crea el registro de auditoría (estado Pendiente) y lo
    /// deja encolado para que el BackgroundService ProcesadorEventosBackgroundService lo procese
    /// de forma desacoplada (evaluación de reglas, ejecución de workflow y reintentos).
    /// </summary>
    public async Task<EventoProcesadoDto> DispararEventoAsync(
        string codigoEvento, string? contextoJson = null, int? idUsuario = null, CancellationToken ct = default)
    {
        var evento = await _eventoRepository.GetByCodigoAsync(codigoEvento.Trim(), ct);
        if (evento == null)
            throw new InvalidOperationException($"No existe un evento con el código '{codigoEvento}'.");

        var procesado = new EventoProcesado
        {
            IdEvento = evento.IdEvento,
            FechaHora = DateTime.UtcNow,
            Estado = "Pendiente",
            IdUsuario = idUsuario,
            Resultado = contextoJson
        };
        await _eventoProcesadoRepository.AddAsync(procesado, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // El procesamiento (reglas, workflow, reintentos) lo realiza el ProcesadorEventosBackgroundService
        // de forma desacoplada. Se devuelve el registro encolado (mapeado directamente desde la
        // entidad rastreada para evitar múltiples instancias con la misma clave en el contexto).
        procesado.Evento = evento;
        return Map(procesado);
    }

    /// <summary>
    /// Procesa un evento ya persistido: evalúa reglas, ejecuta workflows y aplica reintentos.
    /// Invocado por el ProcesadorEventosBackgroundService (desacoplado).
    /// </summary>
    public async Task ProcesarEventoAsync(int idEventoProcesado, CancellationToken ct = default)
    {
        var procesado = await _eventoProcesadoRepository.GetByIdAsync(idEventoProcesado, ct);
        if (procesado == null) return;

        var config = await _configRepository.GetAsync(ct);
        var inicio = DateTime.UtcNow;

        // Marcar en proceso / reintentando
        if (procesado.Estado == "Reintentando")
            procesado.Estado = "Reintentando";
        else if (procesado.Estado == "Pendiente" || procesado.Estado == "EnProceso")
            procesado.Estado = "EnProceso";

        try
        {
            var reglas = (await _reglaRepository.GetByEventoAsync(procesado.IdEvento, ct))
                .Where(r => r.Activa)
                .OrderByDescending(r => r.Prioridad)
                .ToList();

            if (reglas.Count == 0)
            {
                procesado.Estado = "Completado";
                procesado.Resultado = "No hay reglas activas asociadas a este evento.";
                await _eventoProcesadoRepository.UpdateAsync(procesado, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }

            var contexto = ParseContexto(procesado.Resultado);
            var ejecutadoAlguna = false;
            var errores = new List<string>();

            foreach (var regla in reglas)
            {
                if (ct.IsCancellationRequested) break;

                if (!EvaluarCondicion(regla.Condicion, contexto))
                {
                    _logger.LogInformation("Regla {Id} omitida: condición no cumplida.", regla.IdRegla);
                    continue;
                }

                procesado.IdRegla = regla.IdRegla;
                procesado.IdWorkflow = regla.IdWorkflow;

                var (exito, mensaje) = await EjecutarConReintentosAsync(regla.IdWorkflow, procesado.IdUsuario, config, ct);
                ejecutadoAlguna = true;
                if (!exito) errores.Add($"Regla {regla.IdRegla}: {mensaje}");
            }

            if (errores.Count > 0 && ejecutadoAlguna)
            {
                procesado.Estado = "Error";
                procesado.Resultado = string.Join(" | ", errores);
            }
            else
            {
                procesado.Estado = "Completado";
                procesado.Resultado = ejecutadoAlguna
                    ? "Evento procesado y workflow(s) ejecutado(s) automáticamente."
                    : "Ninguna regla cumplió su condición.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar el evento {Id}.", idEventoProcesado);
            procesado.Estado = "Error";
            procesado.Resultado = ex.Message;
        }
        finally
        {
            procesado.TiempoProcesamiento = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
            await _eventoProcesadoRepository.UpdateAsync(procesado, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task<(bool exito, string mensaje)> EjecutarConReintentosAsync(
        int idWorkflow, int? idUsuario, ConfiguracionEventoMotor config, CancellationToken ct)
    {
        int intentos = config.ReintentosMaximos + 1;
        for (int i = 1; i <= intentos; i++)
        {
            if (i > 1)
            {
                _logger.LogWarning("Reintento {I} del workflow {W} por evento automático.", i, idWorkflow);
                await Task.Delay(Math.Min(config.IntervaloReintentoMs * i, 10000), ct);
            }
            try
            {
                var resultado = await _workflowEngine.EjecutarAsync(
                    idWorkflow, idUsuario ?? 1, null, confirmado: true, cancellationToken: ct);
                if (resultado.Exitoso)
                    return (true, resultado.ResultadoFinal ?? "OK");
                if (i < intentos)
                    continue;
                return (false, resultado.ResultadoFinal ?? "Falló tras reintentos.");
            }
            catch (Exception ex)
            {
                if (i < intentos) continue;
                return (false, ex.Message);
            }
        }
        return (false, "Se agotaron los reintentos.");
    }

    private static Dictionary<string, object?> ParseContexto(string? json)
    {
        var dict = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(json)) return dict;
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            if (parsed != null) return parsed;
        }
        catch (JsonException) { }
        return dict;
    }

    /// <summary>
    /// Evalúa una condición simple de la regla contra el contexto del evento.
    /// Soporta comparaciones: ==, !=, &gt;=, &lt;=, &gt;, &lt; sobre propiedades del contexto.
    /// Si la condición está vacía, siempre devuelve true.
    /// </summary>
    private static bool EvaluarCondicion(string? condicion, Dictionary<string, object?> contexto)
    {
        if (string.IsNullOrWhiteSpace(condicion)) return true;
        try
        {
            var partes = condicion.Split('|').Select(p => p.Trim()).ToList();
            foreach (var expr in partes)
            {
                if (!EvaluarExpresion(expr, contexto)) return false;
            }
            return true;
        }
        catch
        {
            // En caso de condición mal formada, se ejecuta igualmente (fail-open).
            return true;
        }
    }

    private static bool EvaluarExpresion(string expr, Dictionary<string, object?> contexto)
    {
        string op = expr.Contains(">=") ? ">=" :
                    expr.Contains("<=") ? "<=" :
                    expr.Contains("!=") ? "!=" :
                    expr.Contains("==") ? "==" :
                    expr.Contains(">") ? ">" :
                    expr.Contains("<") ? "<" : "=";
        var tokens = expr.Split(new[] { ">=", "<=", "!=", "==", ">", "<", "=" }, 2, StringSplitOptions.None);
        if (tokens.Length < 2) return true;
        var izq = tokens[0].Trim();
        var der = tokens[1].Trim().Trim('\'', '"');

        contexto.TryGetValue(izq, out var rawValor);
        var valor = rawValor?.ToString() ?? string.Empty;

        if (op is ">=" or "<=" or ">" or "<")
        {
            if (!decimal.TryParse(valor, out var a) || !decimal.TryParse(der, out var b)) return false;
            return op switch
            {
                ">=" => a >= b,
                "<=" => a <= b,
                ">" => a > b,
                "<" => a < b,
                _ => false
            };
        }
        return op switch
        {
            "!=" => !string.Equals(valor, der, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(valor, der, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static EventoProcesadoDto Map(EventoProcesado e) => new()
    {
        IdEventoProcesado = e.IdEventoProcesado,
        IdEvento = e.IdEvento,
        CodigoEvento = e.Evento?.Codigo ?? string.Empty,
        NombreEvento = e.Evento?.Nombre ?? string.Empty,
        FechaHora = e.FechaHora,
        Estado = e.Estado,
        Resultado = e.Resultado,
        TiempoProcesamiento = e.TiempoProcesamiento,
        IdRegla = e.IdRegla,
        IdWorkflow = e.IdWorkflow
    };
}
