using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IWorkflowPasoRepository _pasoRepository;
    private readonly IWorkflowEjecucionRepository _ejecucionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowService(
        IWorkflowRepository workflowRepository,
        IWorkflowPasoRepository pasoRepository,
        IWorkflowEjecucionRepository ejecucionRepository,
        IUnitOfWork unitOfWork)
    {
        _workflowRepository = workflowRepository;
        _pasoRepository = pasoRepository;
        _ejecucionRepository = ejecucionRepository;
        _unitOfWork = unitOfWork;
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
        w.Nombre = request.Nombre;
        w.Descripcion = request.Descripcion;
        w.Disparadores = request.Disparadores;

        // Sincronizar pasos: se reemplaza la colección completa por la enviada.
        // IMPORTANTE: GetByIdAsync usa AsNoTracking, por lo que 'w' está desadjuntado y
        // 'w.Pasos' NO es la fuente fiable de verdad. Por eso se eliminan y re-crean los
        // pasos EXCLUSIVAMENTE a través de _pasoRepository (mismo DbContext que el SaveChanges),
        // con la FK IdWorkflow explícita, y NO se muta w.Pasos (evita que Update(w) re-adjunto
        // los pasos viejos y duplique filas en cada guardado: 2 -> 4 -> 8 ...).
        // SEGURIDAD: si request.Pasos es nulo o vacío, NO se borra nada (se conservan los
        // pasos existentes). Esto evita perder todos los pasos cuando el formulario no envía
        // la lista (p.ej. un bindeo fallido del lado del cliente).
        if (request.Pasos != null && request.Pasos.Count > 0)
        {
            await _pasoRepository.DeleteByWorkflowAsync(id, ct);
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

        await _workflowRepository.UpdateAsync(w, ct);
        await _unitOfWork.SaveChangesAsync(ct);
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
        // Borrar pasos (ExecuteDelete, sin RowVersion).
        await _pasoRepository.DeleteByWorkflowAsync(id, ct);
        // Borrar el workflow por ID directo (ExecuteDelete, sin RowVersion) para
        // evitar DbUpdateConcurrencyException por el RowVersion stale de la entidad
        // AsNoTracking cuando el background service (Quartz) la actualiza.
        await _workflowRepository.DeleteByIdAsync(id, ct);
        await _unitOfWork.SaveChangesAsync(ct);
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
