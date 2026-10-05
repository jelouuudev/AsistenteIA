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
    Task<IEnumerable<DocumentoContenidoResumen>> GetContenidosIndexadosAsync(int maxCharsPorDocumento);
    /// <summary>
    /// Nombre de archivo de la versión vigente de cada documento Activo.
    /// Una sola consulta: alimenta el rescate por mención tolerante a typos
    /// (el usuario suele escribir el nombre del archivo que subió).
    /// </summary>
    Task<IEnumerable<(int IdDocumento, string NombreArchivo)>> GetNombresArchivoAsync();
    Task AddAsync(Documento documento);
    void Update(Documento documento);
}
