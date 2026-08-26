using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Eventos;

public class TareaProgramadaService : ITareaProgramadaService
{
    private readonly ITareaProgramadaRepository _repository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TareaProgramadaService(
        ITareaProgramadaRepository repository,
        IWorkflowRepository workflowRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _workflowRepository = workflowRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<TareaProgramadaDto>> ObtenerTodosAsync(CancellationToken ct = default)
        => (await _repository.GetAllAsync(ct)).Select(t => Map(t));

    public async Task<TareaProgramadaDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var t = await _repository.GetByIdAsync(id, ct);
        return t == null ? null : Map(t);
    }

    public async Task<TareaProgramadaDto> CrearAsync(CrearTareaProgramadaRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExpresionCron))
            throw new ArgumentException("La expresión Cron es obligatoria.");
        var wf = await _workflowRepository.GetByIdAsync(request.IdWorkflow, ct)
            ?? throw new KeyNotFoundException("El workflow asociado no existe.");

        var tarea = new TareaProgramada
        {
            Nombre = request.Nombre.Trim(),
            ExpresionCron = request.ExpresionCron.Trim(),
            IdWorkflow = request.IdWorkflow,
            Activa = request.Activa,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = request.UsuarioCreacion
        };
        await _repository.AddAsync(tarea, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(tarea, wf);
    }

    public async Task ActualizarAsync(int id, ActualizarTareaProgramadaRequest request, CancellationToken ct = default)
    {
        var tarea = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Tarea no encontrada.");
        tarea.Nombre = request.Nombre.Trim();
        tarea.ExpresionCron = request.ExpresionCron.Trim();
        tarea.IdWorkflow = request.IdWorkflow;
        tarea.Activa = request.Activa;
        await _repository.UpdateAsync(tarea, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task CambiarEstadoAsync(int id, bool activa, CancellationToken ct = default)
    {
        var tarea = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Tarea no encontrada.");
        tarea.Activa = activa;
        await _repository.UpdateAsync(tarea, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var tarea = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Tarea no encontrada.");
        await _repository.DeleteAsync(tarea, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static TareaProgramadaDto Map(TareaProgramada t, Workflow? w = null) => new()
    {
        IdTarea = t.IdTarea,
        Nombre = t.Nombre,
        ExpresionCron = t.ExpresionCron,
        IdWorkflow = t.IdWorkflow,
        NombreWorkflow = w?.Nombre ?? t.Workflow?.Nombre ?? string.Empty,
        Activa = t.Activa,
        UltimaEjecucion = t.UltimaEjecucion,
        ProximaEjecucion = t.ProximaEjecucion
    };
}
