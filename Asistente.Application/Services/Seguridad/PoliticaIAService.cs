using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Seguridad;

public class PoliticaIAService : IPoliticaIAService
{
    private readonly IPoliticaIARepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PoliticaIAService(IPoliticaIARepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PoliticaIADto>> ObtenerTodasAsync(CancellationToken ct = default)
        => (await _repository.GetActivasAsync(ct)).Select(Map);

    public async Task<PoliticaIADto?> ObtenerPorTipoAsync(string tipo, CancellationToken ct = default)
    {
        var p = await _repository.GetByTipoAsync(tipo, ct);
        return p == null ? null : Map(p);
    }

    public async Task CrearAsync(CrearPoliticaIARequest request, CancellationToken ct = default)
    {
        await _repository.AddAsync(new PoliticaIA
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Tipo = request.Tipo,
            Valor = request.Valor,
            Activa = request.Activa
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(int id, CrearPoliticaIARequest request, CancellationToken ct = default)
    {
        var existente = await _repository.GetByTipoAsync(request.Tipo, ct);
        if (existente == null) return;
        existente.Nombre = request.Nombre;
        existente.Descripcion = request.Descripcion;
        existente.Valor = request.Valor;
        existente.Activa = request.Activa;
        existente.FechaActualizacion = System.DateTime.UtcNow;
        _repository.UpdateAsync(existente, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static PoliticaIADto Map(PoliticaIA p) => new()
    {
        IdPolitica = p.IdPolitica,
        Nombre = p.Nombre,
        Descripcion = p.Descripcion,
        Tipo = p.Tipo,
        Valor = p.Valor,
        Activa = p.Activa
    };
}
