using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IProcesamientoDocumentalService
{
    Task<DocumentoProcesadoDto?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<DocumentoProcesadoDto>> ObtenerTodosAsync();
    Task<IEnumerable<DocumentoProcesadoDto>> ObtenerPorEstadoAsync(string estado);
    Task<DashboardProcesamientoDto> ObtenerDashboardAsync();
    Task<DocumentoProcesadoDto?> ObtenerPorVersionIdAsync(int versionId);
    Task<IEnumerable<DocumentoChunkDto>> ObtenerChunksAsync(int procesadoId);
    Task ProcesarDocumentoAsync(int versionId);
    Task ReprocesarDocumentoAsync(int procesadoId);
    Task<int> RepararDocumentosConChunksFaltantesAsync();
    Task<IEnumerable<DocumentoProcesadoDto>> ObtenerDocumentosPendientesAsync();
}
