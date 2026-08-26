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

public class EventoEmpresarialService : IEventoEmpresarialService
{
    private readonly IEventoEmpresarialRepository _repository;
    private readonly IReglaEventoRepository _reglaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EventoEmpresarialService(
        IEventoEmpresarialRepository repository,
        IReglaEventoRepository reglaRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _reglaRepository = reglaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<EventoEmpresarialDto>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var eventos = await _repository.GetAllAsync(ct);
        var reglas = await _reglaRepository.GetAllAsync(ct);
        return eventos.Select(e => new EventoEmpresarialDto
        {
            IdEvento = e.IdEvento,
            Codigo = e.Codigo,
            Nombre = e.Nombre,
            Descripcion = e.Descripcion,
            Categoria = e.Categoria,
            Activo = e.Activo,
            FechaCreacion = e.FechaCreacion,
            CantidadReglas = reglas.Count(r => r.IdEvento == e.IdEvento)
        });
    }

    public async Task<EventoEmpresarialDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _repository.GetByIdAsync(id, ct);
        if (e == null) return null;
        var reglas = await _reglaRepository.GetByEventoAsync(id, ct);
        return new EventoEmpresarialDto
        {
            IdEvento = e.IdEvento,
            Codigo = e.Codigo,
            Nombre = e.Nombre,
            Descripcion = e.Descripcion,
            Categoria = e.Categoria,
            Activo = e.Activo,
            FechaCreacion = e.FechaCreacion,
            CantidadReglas = reglas.Count()
        };
    }

    public async Task<EventoEmpresarialDto> CrearAsync(CrearEventoEmpresarialRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Nombre))
            throw new ArgumentException("El código y el nombre del evento son obligatorios.");

        if (await _repository.GetByCodigoAsync(request.Codigo.Trim(), ct) != null)
            throw new InvalidOperationException($"Ya existe un evento con el código '{request.Codigo}'.");

        var evento = new EventoEmpresarial
        {
            Codigo = request.Codigo.Trim(),
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            Categoria = request.Categoria,
            Activo = request.Activo,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacion = request.UsuarioCreacion
        };
        await _repository.AddAsync(evento, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new EventoEmpresarialDto
        {
            IdEvento = evento.IdEvento,
            Codigo = evento.Codigo,
            Nombre = evento.Nombre,
            Descripcion = evento.Descripcion,
            Categoria = evento.Categoria,
            Activo = evento.Activo,
            FechaCreacion = evento.FechaCreacion
        };
    }

    public async Task ActualizarAsync(int id, ActualizarEventoEmpresarialRequest request, CancellationToken ct = default)
    {
        var evento = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Evento no encontrado.");
        evento.Nombre = request.Nombre.Trim();
        evento.Descripcion = request.Descripcion?.Trim();
        evento.Categoria = request.Categoria;
        evento.Activo = request.Activo;
        // El código no se modifica para preservar los disparadores.
        await _repository.UpdateAsync(evento, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        var evento = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Evento no encontrado.");
        evento.Activo = activo;
        await _repository.UpdateAsync(evento, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var evento = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Evento no encontrado.");
        await _reglaRepository.DeleteByEventoAsync(id, ct);
        await _repository.DeleteAsync(evento, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<ReglaEventoDto>> ObtenerReglasAsync(int idEvento, CancellationToken ct = default)
    {
        var reglas = await _reglaRepository.GetByEventoAsync(idEvento, ct);
        return reglas.Select(MapRegla);
    }

    private static ReglaEventoDto MapRegla(ReglaEvento r) => new()
    {
        IdRegla = r.IdRegla,
        IdEvento = r.IdEvento,
        NombreEvento = r.Evento?.Nombre ?? string.Empty,
        IdWorkflow = r.IdWorkflow,
        NombreWorkflow = r.Workflow?.Nombre ?? string.Empty,
        Condicion = r.Condicion,
        Prioridad = r.Prioridad,
        Activa = r.Activa
    };
}
