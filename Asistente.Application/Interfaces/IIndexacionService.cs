using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IIndexacionService
{
    Task<DocumentoIndexadoDto?> ObtenerPorIdAsync(int id);
    Task<DocumentoIndexadoDto?> ObtenerPorProcesadoIdAsync(int procesadoId);
    Task<IEnumerable<DocumentoIndexadoDto>> ObtenerTodosAsync();
    Task<IEnumerable<DocumentoIndexadoDto>> ObtenerPorEstadoAsync(string estado);
    Task<DashboardIndexacionDto> ObtenerDashboardAsync();
    Task IndexarDocumentoAsync(int documentoProcesadoId);
    Task ReindexarDocumentoAsync(int documentoProcesadoId);
    Task ReindexarPorDocumentoAsync(int idDocumento);
    Task ReindexarCategoriaAsync(int categoriaId);
    Task ReindexarTodosAsync();
    Task EliminarIndiceAsync(int documentoProcesadoId);
    Task LimpiarVectorStoreAsync();
}
