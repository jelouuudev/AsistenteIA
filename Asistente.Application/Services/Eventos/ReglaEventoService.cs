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

public class ReglaEventoService : IReglaEventoService
{
    private readonly IReglaEventoRepository _repository;
    private readonly IEventoEmpresarialRepository _eventoRepository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReglaEventoService(
        IReglaEventoRepository repository,
        IEventoEmpresarialRepository eventoRepository,
        IWorkflowRepository workflowRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _eventoRepository = eventoRepository;
        _workflowRepository = workflowRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<ReglaEventoDto>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var reglas = await _repository.GetAllAsync(ct);
        return reglas.Select(r => Map(r));
    }

    public async Task<ReglaEventoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var r = await _repository.GetByIdAsync(id, ct);
        return r == null ? null : Map(r);
    }

    public async Task<ReglaEventoDto> CrearAsync(CrearReglaEventoRequest request, CancellationToken ct = default)
    {
        var evento = await _eventoRepository.GetByIdAsync(request.IdEvento, ct)
            ?? throw new KeyNotFoundException("El evento origen no existe.");
        var workflow = await _workflowRepository.GetByIdAsync(request.IdWorkflow, ct)
            ?? throw new KeyNotFoundException("El workflow asociado no existe.");

        var regla = new ReglaEvento
        {
            IdEvento = request.IdEvento,
            IdWorkflow = request.IdWorkflow,
            Condicion = string.IsNullOrWhiteSpace(request.Condicion) ? null : request.Condicion.Trim(),
            Prioridad = request.Prioridad,
            Activa = request.Activa,
            FechaCreacion = DateTime.UtcNow
        };
        await _repository.AddAsync(regla, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Map(regla, evento, workflow);
    }

    public async Task ActualizarAsync(int id, ActualizarReglaEventoRequest request, CancellationToken ct = default)
    {
        var regla = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Regla no encontrada.");
        regla.IdWorkflow = request.IdWorkflow;
        regla.Condicion = string.IsNullOrWhiteSpace(request.Condicion) ? null : request.Condicion.Trim();
        regla.Prioridad = request.Prioridad;
        regla.Activa = request.Activa;
        await _repository.UpdateAsync(regla, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task CambiarEstadoAsync(int id, bool activa, CancellationToken ct = default)
    {
        var regla = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Regla no encontrada.");
        regla.Activa = activa;
        await _repository.UpdateAsync(regla, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var regla = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Regla no encontrada.");
        await _repository.DeleteAsync(regla, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static ReglaEventoDto Map(ReglaEvento r, EventoEmpresarial? e = null, Workflow? w = null) => new()
    {
        IdRegla = r.IdRegla,
        IdEvento = r.IdEvento,
        NombreEvento = e?.Nombre ?? r.Evento?.Nombre ?? string.Empty,
        IdWorkflow = r.IdWorkflow,
        NombreWorkflow = w?.Nombre ?? r.Workflow?.Nombre ?? string.Empty,
        Condicion = r.Condicion,
        Prioridad = r.Prioridad,
        Activa = r.Activa
    };
}
