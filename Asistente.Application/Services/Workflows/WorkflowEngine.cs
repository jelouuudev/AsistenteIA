using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Workflows;

/// <summary>
/// Resultado de la ejecución de un flujo de trabajo.
/// </summary>
public class WorkflowExecutionResult
{
    public int IdEjecucion { get; set; }
    public bool Exitoso { get; set; }
    public string Estado { get; set; } = "EnProceso";
    public string? ResultadoFinal { get; set; }
    public long TiempoTotalMs { get; set; }
    public List<WorkflowPasoResultado> Pasos { get; set; } = new();

    /// <summary>Verdadero cuando un paso requiere confirmación del usuario antes de continuar.</summary>
    public bool RequiereConfirmacion { get; set; }
    public string? PasoPendienteConfirmacion { get; set; }
}

public class WorkflowPasoResultado
{
    public int IdPaso { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Herramienta { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
    public string? Resultado { get; set; }
    public long TiempoMs { get; set; }
    public int Intentos { get; set; }
}

/// <summary>
/// Motor de Flujos de Trabajo (Workflow Engine) - ETAPA 12.
///
/// Responsabilidades (Requerimiento Funcional - Actividad 1):
///  - Cargar la definición del flujo.
///  - Ejecutar cada paso en orden (secuencial).
///  - Administrar el contexto compartido entre pasos (Actividad 4).
///  - Gestionar errores y reintentos configurables (Actividad 5).
///  - Solicitar confirmación del usuario para pasos sensibles (Actividad 6).
///  - Registrar auditoría completa de la ejecución y de cada paso (Actividad 7).
///
/// El motor es totalmente desacoplado de las herramientas: delega la ejecución de cada
/// paso al IToolOrchestrator, reutilizando así la autorización y auditoría de herramientas.
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IWorkflowEjecucionRepository _ejecucionRepository;
    private readonly IWorkflowPasoEjecucionRepository _pasoEjecucionRepository;
    private readonly IConfiguracionWorkflowRepository _configRepository;
    private readonly IToolOrchestrator _toolOrchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowRepository workflowRepository,
        IWorkflowEjecucionRepository ejecucionRepository,
        IWorkflowPasoEjecucionRepository pasoEjecucionRepository,
        IConfiguracionWorkflowRepository configRepository,
        IToolOrchestrator toolOrchestrator,
        IUnitOfWork unitOfWork,
        ILogger<WorkflowEngine> logger)
    {
        _workflowRepository = workflowRepository;
        _ejecucionRepository = ejecucionRepository;
        _pasoEjecucionRepository = pasoEjecucionRepository;
        _configRepository = configRepository;
        _toolOrchestrator = toolOrchestrator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<WorkflowExecutionResult> EjecutarAsync(
        int idWorkflow, int idUsuario, int? idAsistente, bool confirmado = false,
        int? idEjecucionExistente = null, CancellationToken cancellationToken = default)
    {
        var inicio = DateTime.UtcNow;
        var config = await _configRepository.GetAsync(cancellationToken);
        var workflow = await _workflowRepository.GetByIdAsync(idWorkflow, cancellationToken);

        if (workflow == null)
            return new WorkflowExecutionResult { Exitoso = false, Estado = "Error", ResultadoFinal = "El flujo de trabajo no existe." };

        if (workflow.Estado != EstadoWorkflow.Activo)
            return new WorkflowExecutionResult { Exitoso = false, Estado = "Error", ResultadoFinal = "El flujo de trabajo no está activo." };

        var pasos = workflow.Pasos.OrderBy(p => p.Orden).ToList();
        if (pasos.Count == 0)
            return new WorkflowExecutionResult { Exitoso = false, Estado = "Error", ResultadoFinal = "El flujo no tiene pasos configurados." };

        // Crear o reanudar la ejecución (para soporte de confirmación en dos fases)
        WorkflowEjecucion ejecucion;
        if (idEjecucionExistente.HasValue)
        {
            ejecucion = await _ejecucionRepository.GetByIdAsync(idEjecucionExistente.Value, cancellationToken)
                       ?? await CrearEjecucionAsync(workflow, idUsuario, idAsistente);
        }
        else
        {
            ejecucion = await CrearEjecucionAsync(workflow, idUsuario, idAsistente);
        }

        var pasosYaEjecutados = (await _pasoEjecucionRepository.GetByEjecucionAsync(ejecucion.IdEjecucion, cancellationToken))
            .ToDictionary(p => p.IdPaso);

        // Contexto compartido: resultado (Contenido) de cada paso indexado por orden y por código de herramienta.
        // 'resultado' contiene el resultado del ÚLTIMO paso ejecutado (para {{resultado}} en el paso siguiente).
        var contexto = new Dictionary<string, string>();
        string? ultimoResultado = null;
        foreach (var pe in pasosYaEjecutados.Values.Where(p => p.Estado == "Exitosa" && !string.IsNullOrEmpty(p.Resultado)).OrderBy(p => p.IdPaso))
        {
            var pasoDef = pasos.FirstOrDefault(p => p.IdPaso == pe.IdPaso);
            if (pasoDef != null)
            {
                contexto[$"Paso{pasoDef.Orden}"] = pe.Resultado!;
                contexto[pasoDef.Herramienta] = pe.Resultado!;
                ultimoResultado = pe.Resultado;
            }
        }
        if (ultimoResultado != null)
            contexto["resultado"] = ultimoResultado;

        var resultado = new WorkflowExecutionResult { IdEjecucion = ejecucion.IdEjecucion };
        var flujoInicio = DateTime.UtcNow;

        foreach (var paso in pasos)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Timeout global del flujo
            if ((DateTime.UtcNow - flujoInicio).TotalMilliseconds > config.TiempoMaximoFlujoMs)
            {
                ejecucion.Estado = "Error";
                ejecucion.ResultadoFinal = "Se excedió el tiempo máximo del flujo.";
                break;
            }

            // Paso ya ejecutado en una fase previa (reanudación tras confirmación)
            if (pasosYaEjecutados.TryGetValue(paso.IdPaso, out var yaEjecutado) && yaEjecutado.Estado == "Exitosa")
            {
                resultado.Pasos.Add(new WorkflowPasoResultado
                {
                    IdPaso = paso.IdPaso,
                    Nombre = paso.Nombre,
                    Herramienta = paso.Herramienta,
                    Exitoso = true,
                    Resultado = yaEjecutado.Resultado,
                    TiempoMs = 0,
                    Intentos = 1
                });
                continue;
            }

            // Confirmación del usuario (Actividad 6)
            if (paso.RequiereConfirmacion && !confirmado && config.ConfirmacionesObligatorias)
            {
                ejecucion.Estado = "RequiereConfirmacion";
                ejecucion.FechaFin = DateTime.UtcNow;
                ejecucion.TiempoTotalMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
                await _ejecucionRepository.UpdateAsync(ejecucion, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                resultado.Estado = "RequiereConfirmacion";
                resultado.RequiereConfirmacion = true;
                resultado.PasoPendienteConfirmacion = paso.Nombre;
                resultado.TiempoTotalMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
                return resultado;
            }

            var pasoResult = await EjecutarPasoAsync(paso, contexto, idUsuario, idAsistente, config, cancellationToken);
            resultado.Pasos.Add(pasoResult);

            // Auditar paso
            var pasoEjec = new WorkflowPasoEjecucion
            {
                IdEjecucion = ejecucion.IdEjecucion,
                IdPaso = paso.IdPaso,
                FechaInicio = DateTime.UtcNow.AddMilliseconds(-pasoResult.TiempoMs),
                FechaFin = DateTime.UtcNow,
                Resultado = pasoResult.Resultado,
                Estado = pasoResult.Exitoso ? "Exitosa" : "Error",
                Observaciones = pasoResult.Exitoso ? null : "Falló tras reintentos."
            };
            await _pasoEjecucionRepository.AddAsync(pasoEjec, cancellationToken);

            if (pasoResult.Exitoso)
            {
                contexto[$"Paso{paso.Orden}"] = pasoResult.Resultado ?? string.Empty;
                contexto[paso.Herramienta] = pasoResult.Resultado ?? string.Empty;
                // Clave de contexto compartido para el paso siguiente ({{resultado}})
                contexto["resultado"] = pasoResult.Resultado ?? string.Empty;
            }
            else
            {
                // Estrategia de error (Actividad 5)
                switch (paso.EstrategiaError)
                {
                    case EstrategiaError.Omitir:
                    case EstrategiaError.RegistrarIncidencia:
                        _logger.LogWarning("Paso '{Paso}' omitido por estrategia {E} en workflow {W}.", paso.Nombre, paso.EstrategiaError, workflow.Codigo);
                        continue;
                    case EstrategiaError.Cancelar:
                    default:
                        ejecucion.Estado = "Error";
                        ejecucion.ResultadoFinal = $"El paso '{paso.Nombre}' falló y canceló el flujo.";
                        break;
                }
                break;
            }
        }

        // Determinar estado final.
        // En reanudación tras confirmación, el estado previo puede ser "RequiereConfirmacion"
        // pero todos los pasos ya se ejecutaron; en ese caso se marca como Exitosa si ninguno falló.
        var huboError = resultado.Pasos.Any(p => !p.Exitoso);
        if (ejecucion.Estado == "RequiereConfirmacion")
        {
            ejecucion.Estado = huboError ? "Error" : "Exitosa";
        }
        else if (ejecucion.Estado == "EnProceso" || string.IsNullOrEmpty(ejecucion.Estado))
        {
            ejecucion.Estado = huboError ? "Error" : "Exitosa";
        }

        if (ejecucion.Estado == "Exitosa")
        {
            ejecucion.ResultadoFinal = string.Join("\n\n",
                resultado.Pasos.Where(p => p.Exitoso && !string.IsNullOrEmpty(p.Resultado))
                    .Select(p => $"[{p.Nombre}] {p.Resultado}"));
        }
        else if (string.IsNullOrEmpty(ejecucion.ResultadoFinal))
        {
            ejecucion.ResultadoFinal = "El flujo finalizó con errores.";
        }

        ejecucion.FechaFin = DateTime.UtcNow;
        ejecucion.TiempoTotalMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
        resultado.Estado = ejecucion.Estado;
        resultado.Exitoso = ejecucion.Estado == "Exitosa";
        resultado.ResultadoFinal = ejecucion.ResultadoFinal;
        resultado.TiempoTotalMs = ejecucion.TiempoTotalMs ?? 0;

        await _ejecucionRepository.UpdateAsync(ejecucion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return resultado;
    }

    private async Task<WorkflowPasoResultado> EjecutarPasoAsync(
        WorkflowPaso paso, Dictionary<string, string> contexto, int idUsuario, int? idAsistente,
        ConfiguracionWorkflow config, CancellationToken cancellationToken)
    {
        var resultado = new WorkflowPasoResultado
        {
            IdPaso = paso.IdPaso,
            Nombre = paso.Nombre,
            Herramienta = paso.Herramienta
        };

        var reintentos = Math.Max(paso.ReintentosMaximos, config.ReintentosMaximos);
        var tiempoMax = paso.TiempoMaximoMs > 0 ? paso.TiempoMaximoMs : config.TiempoMaximoPasoMs;

        for (int intento = 1; intento <= reintentos + 1; intento++)
        {
            resultado.Intentos = intento;
            var inicioPaso = DateTime.UtcNow;

            try
            {
                var parametros = SustituirContexto(paso.Parametros, contexto);
                _logger.LogInformation("Workflow: ejecutando paso '{Paso}' (herramienta {H}, intento {I}).", paso.Nombre, paso.Herramienta, intento);

                var r = await _toolOrchestrator.EjecutarAsync(new ToolExecutionRequest
                {
                    HerramientaCodigo = paso.Herramienta,
                    Parametros = parametros,
                    IdUsuario = idUsuario,
                    IdAsistente = idAsistente,
                    PreguntaOriginal = $"Ejecutado por workflow: {paso.Nombre}"
                }, cancellationToken);

                resultado.TiempoMs = (long)(DateTime.UtcNow - inicioPaso).TotalMilliseconds;

                if (r.Exitoso)
                {
                    resultado.Exitoso = true;
                    resultado.Resultado = r.Contenido;
                    return resultado;
                }

                resultado.Resultado = r.Error;
            }
            catch (Exception ex)
            {
                resultado.TiempoMs = (long)(DateTime.UtcNow - inicioPaso).TotalMilliseconds;
                resultado.Resultado = ex.Message;
                _logger.LogWarning(ex, "Error en paso '{Paso}' (intento {I}).", paso.Nombre, intento);
            }

            if (intento <= reintentos)
                await Task.Delay(Math.Min(500 * intento, 2000), cancellationToken);
        }

        return resultado;
    }

    /// <summary>
    /// Sustituye los tokens de contexto compartido ({{resultado}}, {{PasoN}}, {{Herramienta}})
    /// dentro de los parámetros JSON del paso.
    /// </summary>
    private static Dictionary<string, object?> SustituirContexto(string? parametrosJson, Dictionary<string, string> contexto)
    {
        var dict = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(parametrosJson))
            return dict;

        try
        {
            var source = JsonSerializer.Deserialize<Dictionary<string, object?>>(parametrosJson);
            if (source == null) return dict;

            foreach (var kv in source)
            {
                if (kv.Value is string s)
                {
                    var valor = s;
                    foreach (var ctx in contexto)
                        valor = valor.Replace($"{{{{{ctx.Key}}}}}", ctx.Value, StringComparison.OrdinalIgnoreCase);
                    dict[kv.Key] = valor;
                }
                else if (kv.Value is JsonElement je && je.ValueKind == JsonValueKind.String)
                {
                    var valor = je.GetString() ?? string.Empty;
                    foreach (var ctx in contexto)
                        valor = valor.Replace($"{{{{{ctx.Key}}}}}", ctx.Value, StringComparison.OrdinalIgnoreCase);
                    dict[kv.Key] = valor;
                }
                else
                {
                    dict[kv.Key] = kv.Value;
                }
            }
        }
        catch (JsonException)
        {
            // Si no es JSON válido, devolver vacío; el orquestador manejará la falta de parámetros.
        }
        return dict;
    }

    private async Task<WorkflowEjecucion> CrearEjecucionAsync(Workflow workflow, int idUsuario, int? idAsistente)
    {
        var ejecucion = new WorkflowEjecucion
        {
            IdWorkflow = workflow.IdWorkflow,
            IdUsuario = idUsuario,
            IdAsistente = idAsistente,
            FechaInicio = DateTime.UtcNow,
            Estado = "EnProceso"
        };
        await _ejecucionRepository.AddAsync(ejecucion);
        await _unitOfWork.SaveChangesAsync();
        return ejecucion;
    }

    public async Task<WorkflowExecutionResult?> ReanudarPendienteConfirmacionAsync(
        int idUsuario, int? idAsistente, CancellationToken cancellationToken = default)
    {
        var pendiente = await _ejecucionRepository.GetPendienteConfirmacionAsync(idUsuario, cancellationToken);
        if (pendiente == null)
            return null;

        return await EjecutarAsync(
            pendiente.IdWorkflow, idUsuario, idAsistente,
            confirmado: true, idEjecucionExistente: pendiente.IdEjecucion,
            cancellationToken: cancellationToken);
    }
}
