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
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ILogger<EventoMotorService> _logger;

    public EventoMotorService(
        IEventoEmpresarialRepository eventoRepository,
        IReglaEventoRepository reglaRepository,
        IEventoProcesadoRepository eventoProcesadoRepository,
        IConfiguracionEventoMotorRepository configRepository,
        IWorkflowEngine workflowEngine,
        IUnitOfWork unitOfWork,
        IUsuarioRepository usuarioRepository,
        IAuditoriaService auditoriaService,
        ILogger<EventoMotorService> logger)
    {
        _eventoRepository = eventoRepository;
        _reglaRepository = reglaRepository;
        _eventoProcesadoRepository = eventoProcesadoRepository;
        _configRepository = configRepository;
        _workflowEngine = workflowEngine;
        _unitOfWork = unitOfWork;
        _usuarioRepository = usuarioRepository;
        _auditoriaService = auditoriaService;
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
        if (!evento.Activo)
            throw new InvalidOperationException($"El evento '{codigoEvento}' está desactivado.");

        var procesado = new EventoProcesado
        {
            IdEvento = evento.IdEvento,
            FechaHora = DateTime.UtcNow,
            Estado = "Pendiente",
            IdUsuario = idUsuario,
            // El contexto del disparo se preserva en su propia columna: Resultado
            // se sobrescribe durante el procesamiento y el dato original se perdería.
            ContextoDisparo = contextoJson,
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

        // Tiempo máximo por evento: cancela la ejecución de workflows si se excede.
        var tiempoMaxMs = Math.Max(config.TiempoMaximoEventoMs, 1000);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(tiempoMaxMs);
        var ctt = timeoutCts.Token;

        // Actividad 4: verificar permisos — el usuario dueño debe existir y estar activo.
        if (procesado.IdUsuario.HasValue)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(procesado.IdUsuario.Value);
            if (usuario == null || !usuario.Activo)
            {
                procesado.Estado = "Error";
                procesado.Resultado = "Usuario sin permiso o inactivo para la ejecución automática.";
                await _eventoProcesadoRepository.UpdateAsync(procesado, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                await RegistrarIncidenteAsync(procesado, ct);
                return;
            }
        }

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

            // Se lee del campo preservado (con fallback a Resultado para filas
            // disparadas antes de existir la columna ContextoDisparo).
            var contexto = ParseContexto(procesado.ContextoDisparo ?? procesado.Resultado);
            var ejecutadoAlguna = false;
            var errores = new List<string>();

            foreach (var regla in reglas)
            {
                if (ctt.IsCancellationRequested) break;

                if (!EvaluarCondicion(regla.Condicion, contexto))
                {
                    _logger.LogInformation("Regla {Id} omitida: condición no cumplida.", regla.IdRegla);
                    continue;
                }

                procesado.IdRegla = regla.IdRegla;
                procesado.IdWorkflow = regla.IdWorkflow;

                // El contexto del disparo viaja al workflow para {{Clave}} en los pasos.
                var contextoInicial = contexto.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value?.ToString() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
                var (exito, mensaje) = await EjecutarConReintentosAsync(regla.IdWorkflow, procesado.IdUsuario, config, contextoInicial, ctt);
                ejecutadoAlguna = true;
                if (!exito) errores.Add($"Regla {regla.IdRegla}: {mensaje}");
            }

            if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested
                && errores.Any(e => e.Contains("Tiempo máximo")))
            {
                procesado.Estado = "Error";
                procesado.Resultado = $"Tiempo máximo por evento excedido ({tiempoMaxMs} ms).";
                await _eventoProcesadoRepository.UpdateAsync(procesado, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                await RegistrarIncidenteAsync(procesado, ct);
                return;
            }

            if (errores.Count > 0 && ejecutadoAlguna)
            {
                procesado.Estado = "Error";
                procesado.Resultado = string.Join(" | ", errores);
                await _eventoProcesadoRepository.UpdateAsync(procesado, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                // Actividad 7: registrar el incidente y asociarlo a la ejecución.
                await RegistrarIncidenteAsync(procesado, ct);
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
            await RegistrarIncidenteAsync(procesado, ct);
        }
        finally
        {
            procesado.TiempoProcesamiento = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
            await _eventoProcesadoRepository.UpdateAsync(procesado, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Actividad 7: registra el incidente en auditoría, asociado al evento procesado.
    /// Nunca interrumpe el procesamiento.
    /// </summary>
    private async Task RegistrarIncidenteAsync(EventoProcesado procesado, CancellationToken ct)
    {
        try
        {
            await _auditoriaService.RegistrarActividadAsync(
                procesado.IdUsuario ?? 0,
                "Eventos",
                "ErrorAutomatico",
                $"Incidente en evento procesado {procesado.IdEventoProcesado} " +
                $"(evento {procesado.IdEvento}, regla {procesado.IdRegla?.ToString() ?? "-"}, " +
                $"workflow {procesado.IdWorkflow?.ToString() ?? "-"}): {procesado.Resultado}",
                null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar el incidente del evento {Id}.", procesado.IdEventoProcesado);
        }
    }

    private async Task<(bool exito, string mensaje)> EjecutarConReintentosAsync(
        int idWorkflow, int? idUsuario, ConfiguracionEventoMotor config,
        Dictionary<string, string>? contextoInicial, CancellationToken ct)
    {
        int intentos = config.ReintentosMaximos + 1;
        for (int i = 1; i <= intentos; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (i > 1)
            {
                _logger.LogWarning("Reintento {I} del workflow {W} por evento automático.", i, idWorkflow);
                await Task.Delay(Math.Min(Math.Max(config.IntervaloReintentoMs, 0) * i, 10000), ct);
            }
            try
            {
                var resultado = await _workflowEngine.EjecutarAsync(
                    idWorkflow, idUsuario ?? 1, null, confirmado: true,
                    contextoInicial: contextoInicial, cancellationToken: ct);
                if (resultado.Exitoso)
                    return (true, resultado.ResultadoFinal ?? "OK");
                if (i < intentos)
                    continue;
                return (false, resultado.ResultadoFinal ?? "Falló tras reintentos.");
            }
            catch (OperationCanceledException)
            {
                return (false, $"Tiempo máximo por evento excedido ({Math.Max(config.TiempoMaximoEventoMs, 1000)} ms).");
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
        ContextoDisparo = e.ContextoDisparo,
        TiempoProcesamiento = e.TiempoProcesamiento,
        IdRegla = e.IdRegla,
        IdWorkflow = e.IdWorkflow
    };
}
