using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Interfaces;

public interface IDocumentoRepository
{
    Task<Documento?> GetByIdAsync(int id);
    Task<Documento?> GetByIdWithVersionesAsync(int id);
    Task<Documento?> GetByCodigoAsync(string codigo);
    Task<IEnumerable<Documento>> GetAllAsync();
    Task<IEnumerable<Documento>> GetFilteredAsync(string? nombre, int? idCategoria, EstadoDocumento? estado, DateTime? fechaDesde, DateTime? fechaHasta);
    Task AddAsync(Documento documento);
    void Update(Documento documento);
}
