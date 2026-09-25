using Asistente.Application.Interfaces;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IWorkflowPasoRepository _pasoRepository;
    private readonly IWorkflowEjecucionRepository _ejecucionRepository;
    private readonly IConfiguracionWorkflowRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowEngine _workflowEngine;

    public WorkflowService(
        IWorkflowRepository workflowRepository,
        IWorkflowPasoRepository pasoRepository,
        IWorkflowEjecucionRepository ejecucionRepository,
        IConfiguracionWorkflowRepository configRepository,
        IUnitOfWork unitOfWork,
        IWorkflowEngine workflowEngine)
    {
        _workflowRepository = workflowRepository;
        _pasoRepository = pasoRepository;
        _ejecucionRepository = ejecucionRepository;
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _workflowEngine = workflowEngine;
    }

    /// <summary>
    /// Valida el límite de pasos por workflow (Configuración). 0 o negativo = sin límite.
    /// </summary>
    private async Task ValidarLimitePasosAsync(int cantidad, CancellationToken ct)
    {
        var config = await _configRepository.GetAsync(ct);
        var limite = config?.LimitePasosPorWorkflow ?? 0;
        if (limite > 0 && cantidad > limite)
            throw new InvalidOperationException(
                $"El flujo supera el límite de {limite} pasos por workflow (tiene {cantidad}).");
    }

    public async Task<IEnumerable<WorkflowDto>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var lista = await _workflowRepository.GetAllAsync(ct);
        return lista.Select(Mapear);
    }

    public async Task<WorkflowDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var w = await _workflowRepository.GetByIdAsync(id, ct);
        return w == null ? null : Mapear(w);
    }

    public async Task<WorkflowDto> CrearAsync(CrearWorkflowRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Codigo))
            throw new InvalidOperationException("El nombre y el código son obligatorios.");

        var existente = await _workflowRepository.GetByCodigoAsync(request.Codigo, ct);
        if (existente != null)
            throw new InvalidOperationException($"Ya existe un flujo con el código '{request.Codigo}'.");

        await ValidarLimitePasosAsync(request.Pasos?.Count ?? 0, ct);

        var workflow = new Workflow
        {
            Nombre = request.Nombre,
            Codigo = request.Codigo,
            Descripcion = request.Descripcion,
            Disparadores = request.Disparadores,
            Version = 1,
            Estado = EstadoWorkflow.Borrador,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = request.UsuarioCreacion
        };

        if (request.Pasos != null)
        {
            foreach (var p in request.Pasos.OrderBy(x => x.Orden))
            {
                workflow.Pasos.Add(new WorkflowPaso
                {
                    Orden = p.Orden,
                    Nombre = p.Nombre,
                    Herramienta = p.Herramienta,
                    Parametros = p.Parametros,
                    RequiereConfirmacion = p.RequiereConfirmacion,
                    ReintentosMaximos = p.ReintentosMaximos,
                    TiempoMaximoMs = p.TiempoMaximoMs,
                    EstrategiaError = Enum.TryParse<EstrategiaError>(p.EstrategiaError, true, out var e) ? e : EstrategiaError.Cancelar
                });
            }
        }

        await _workflowRepository.AddAsync(workflow, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Mapear(await _workflowRepository.GetByIdAsync(workflow.IdWorkflow, ct) ?? workflow);
    }

    public async Task ActualizarAsync(int id, ActualizarWorkflowRequest request, CancellationToken ct = default)
    {
        var w = await _workflowRepository.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException("Flujo no encontrado.");
        if (request.Pasos != null && request.Pasos.Count > 0)
            await ValidarLimitePasosAsync(request.Pasos.Count, ct);
        w.Nombre = request.Nombre;
        w.Descripcion = request.Descripcion;
        w.Disparadores = request.Disparadores;

        // Sincronizar pasos: se reemplaza la colección completa por la enviada de forma
        // ATÓMICA (todo o nada en un solo SaveChanges). Antes se usaba ExecuteDelete
        // (confirma inmediato) y si el SaveChanges posterior fallaba, los pasos quedaban
        // borrados sin reemplazo. Ahora se marcan borrados con seguimiento (RemoveRange) y
        // todo se confirma junto: ante cualquier error no se pierde nada.
        // SEGURIDAD: si request.Pasos es nulo o vacío, NO se toca nada (se conservan los
        // pasos existentes). Esto evita perder todos los pasos cuando el formulario no envía
        // la lista (p.ej. un bindeo fallido del lado del cliente).
        if (request.Pasos != null && request.Pasos.Count > 0)
        {
            var existentes = await _pasoRepository.GetTrackedByWorkflowAsync(id, ct) ?? new List<WorkflowPaso>();
            _pasoRepository.RemoveRange(existentes);
            foreach (var p in request.Pasos.OrderBy(x => x.Orden))
            {
                await _pasoRepository.AddAsync(new WorkflowPaso
                {
                    IdWorkflow = w.IdWorkflow,
                    Orden = p.Orden,
                    Nombre = p.Nombre,
                    Herramienta = p.Herramienta,
                    Parametros = p.Parametros,
                    RequiereConfirmacion = p.RequiereConfirmacion,
                    ReintentosMaximos = p.ReintentosMaximos,
                    TiempoMaximoMs = p.TiempoMaximoMs,
                    EstrategiaError = Enum.TryParse<EstrategiaError>(p.EstrategiaError, true, out var e) ? e : EstrategiaError.Cancelar
                }, ct);
            }
        }

        // w.Pasos viene poblado del GetById (Include): hay que vaciarlo antes del
        // Update o EF re-adjunta los pasos viejos como Modified, los "resucita" y cada
        // guardado duplica filas (2 -> 4 -> 8). Los pasos viajan solo por RemoveRange/Add.
        w.Pasos = new List<WorkflowPaso>();
        await _workflowRepository.UpdateAsync(w, ct);
        try
        {
            // Un solo SaveChanges: borrado+inserción+update son atómicos (todo o nada).
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            // Otro proceso tocó el flujo a la vez: no se perdió nada (transacción
            // revertida), pero hay que recargar e intentarlo de nuevo.
            // (Chequeo por nombre para no referenciar EF Core desde Application.)
            throw new InvalidOperationException(
                "Otro proceso modificó el flujo al mismo tiempo. Recarga la página e intenta guardar de nuevo.", ex);
        }
    }

    public async Task CambiarEstadoAsync(int id, EstadoWorkflow estado, CancellationToken ct = default)
    {
        var w = await _workflowRepository.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException("Flujo no encontrado.");
        w.Estado = estado;
        await _workflowRepository.UpdateAsync(w, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task VersionarAsync(int id, CancellationToken ct = default)
    {
        var w = await _workflowRepository.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException("Flujo no encontrado.");

        // Versionado real: se clona el flujo (y sus pasos) en un NUEVO registro
        // con Version+1, dejando el original intacto para conservar el histórico.
        var nuevaVersion = w.Version + 1;
        var nuevoCodigo = await GenerarCodigoUnicoAsync(w.Codigo, nuevaVersion, ct);

        var clon = new Workflow
        {
            Nombre = $"{w.Nombre} (v{nuevaVersion})",
            Codigo = nuevoCodigo,
            Descripcion = w.Descripcion,
            Disparadores = w.Disparadores,
            Version = nuevaVersion,
            Estado = EstadoWorkflow.Borrador,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = w.UsuarioCreacion,
            Pasos = w.Pasos.Select(p => new WorkflowPaso
            {
                Orden = p.Orden,
                Nombre = p.Nombre,
                Herramienta = p.Herramienta,
                Parametros = p.Parametros,
                RequiereConfirmacion = p.RequiereConfirmacion,
                ReintentosMaximos = p.ReintentosMaximos,
                TiempoMaximoMs = p.TiempoMaximoMs,
                EstrategiaError = p.EstrategiaError
            }).ToList()
        };

        await _workflowRepository.AddAsync(clon, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<string> GenerarCodigoUnicoAsync(string baseCodigo, int version, CancellationToken ct)
    {
        var candidato = $"{baseCodigo}-v{version}";
        var sufijo = 2;
        while (await _workflowRepository.GetByCodigoAsync(candidato, ct) != null)
        {
            candidato = $"{baseCodigo}-v{version}-{sufijo}";
            sufijo++;
        }
        return candidato;
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        // Validar existencia (GetByIdAsync es AsNoTracking, solo lectura).
        var w = await _workflowRepository.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException("Flujo no encontrado.");
        // Las ejecuciones son auditoría (FK restrictiva): no se pueden borrar con el flujo.
        var ejecuciones = await _ejecucionRepository.GetByWorkflowAsync(id, ct);
        if (ejecuciones.Any())
            throw new InvalidOperationException(
                $"No se puede eliminar el flujo '{w.Nombre}' porque tiene {ejecuciones.Count()} ejecucion(es) registrada(s) en auditoría. " +
                "Suspenda el flujo en su lugar para conservar el historial.");
        // Borrar pasos (ExecuteDelete, sin RowVersion).
        await _pasoRepository.DeleteByWorkflowAsync(id, ct);
        // Borrar el workflow por ID directo (ExecuteDelete, sin RowVersion) para
        // evitar DbUpdateConcurrencyException por el RowVersion stale de la entidad
        // AsNoTracking cuando el background service (Quartz) la actualiza.
        await _workflowRepository.DeleteByIdAsync(id, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<WorkflowExecutionResult> EjecutarWorkflowAsync(int id, int idUsuario, Dictionary<string, object>? parametros = null, CancellationToken ct = default)
    {
        return await _workflowEngine.EjecutarAsync(id, idUsuario, null, cancellationToken: ct);
    }

    public async Task<IEnumerable<WorkflowEjecucionDto>> ObtenerEjecucionesAsync(CancellationToken ct = default)
    {
        var ejecuciones = await _ejecucionRepository.GetAllAsync(ct);
        var dtos = new List<WorkflowEjecucionDto>();
        foreach (var e in ejecuciones)
        {
            var wf = await _workflowRepository.GetByIdAsync(e.IdWorkflow, ct);
            dtos.Add(new WorkflowEjecucionDto
            {
                IdEjecucion = e.IdEjecucion,
                IdWorkflow = e.IdWorkflow,
                NombreWorkflow = wf?.Nombre ?? "(eliminado)",
                IdUsuario = e.IdUsuario,
                IdAsistente = e.IdAsistente,
                FechaInicio = e.FechaInicio,
                FechaFin = e.FechaFin,
                Estado = e.Estado,
                TiempoTotalMs = e.TiempoTotalMs,
                ResultadoFinal = e.ResultadoFinal
            });
        }
        return dtos;
    }

    private static WorkflowDto Mapear(Workflow w) => new()
    {
        IdWorkflow = w.IdWorkflow,
        Nombre = w.Nombre,
        Codigo = w.Codigo,
        Descripcion = w.Descripcion,
        Disparadores = w.Disparadores,
        Version = w.Version,
        Estado = w.Estado.ToString(),
        FechaCreacion = w.FechaCreacion,
        UsuarioCreacion = w.UsuarioCreacion,
        CantidadPasos = w.Pasos.Count,
        Pasos = w.Pasos.OrderBy(p => p.Orden).Select(p => new WorkflowPasoDto
        {
            IdPaso = p.IdPaso,
            IdWorkflow = p.IdWorkflow,
            Orden = p.Orden,
            Nombre = p.Nombre,
            Herramienta = p.Herramienta,
            Parametros = p.Parametros,
            RequiereConfirmacion = p.RequiereConfirmacion,
            ReintentosMaximos = p.ReintentosMaximos,
            TiempoMaximoMs = p.TiempoMaximoMs,
            EstrategiaError = p.EstrategiaError.ToString()
        }).ToList()
    };
}

public class ConfiguracionWorkflowService : IConfiguracionWorkflowService
{
    private readonly IConfiguracionWorkflowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfiguracionWorkflowService(IConfiguracionWorkflowRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfiguracionWorkflowDto> ObtenerAsync(CancellationToken ct = default)
    {
        var c = await _repository.GetAsync(ct);
        return new ConfiguracionWorkflowDto
        {
            ReintentosMaximos = c.ReintentosMaximos,
            TiempoMaximoPasoMs = c.TiempoMaximoPasoMs,
            TiempoMaximoFlujoMs = c.TiempoMaximoFlujoMs,
            ConfirmacionesObligatorias = c.ConfirmacionesObligatorias,
            LimitePasosPorWorkflow = c.LimitePasosPorWorkflow
        };
    }

    public async Task GuardarAsync(ConfiguracionWorkflowDto config, CancellationToken ct = default)
    {
        var c = await _repository.GetAsync(ct) ?? new ConfiguracionWorkflow();
        c.ReintentosMaximos = config.ReintentosMaximos;
        c.TiempoMaximoPasoMs = config.TiempoMaximoPasoMs;
        c.TiempoMaximoFlujoMs = config.TiempoMaximoFlujoMs;
        c.ConfirmacionesObligatorias = config.ConfirmacionesObligatorias;
        c.LimitePasosPorWorkflow = config.LimitePasosPorWorkflow;
        await _repository.UpdateAsync(c, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

public static class WorkflowServiceExtensions
{
    /// <summary>
    /// Extensión que ejecuta un workflow usando el motor IWorkflowEngine.
    /// </summary>
    public static async Task<WorkflowExecutionResult> EjecutarWorkflowAsync(
        this IWorkflowService service, int id, int idUsuario, 
        Dictionary<string, object>? parametros = null, CancellationToken ct = default)
    {
        // Si el servicio implementa IWorkflowEngine, lo usamos directamente
        if (service is IWorkflowEngine engine)
        {
            return await engine.EjecutarAsync(id, idUsuario, null, cancellationToken: ct);
        }
        
        throw new InvalidOperationException("El servicio no soporta ejecución de workflows.");
    }
}
