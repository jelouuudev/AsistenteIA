using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IDocumentoService
{
    Task<DocumentoDto?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<DocumentoDto>> ObtenerTodosAsync();
    Task<IEnumerable<DocumentoDto>> ObtenerFiltradosAsync(FiltroDocumentoRequest filtro);
    Task<DocumentoDto> CrearAsync(CrearDocumentoRequest request, int currentUserId, string ipAddress);
    Task<DocumentoDto> ActualizarAsync(int id, ActualizarDocumentoRequest request, int currentUserId, string ipAddress);
    Task ActivarAsync(int id, int currentUserId, string ipAddress);
    Task ArchivarAsync(int id, int currentUserId, string ipAddress);
    Task EliminarAsync(int id, int currentUserId, string ipAddress);

    // Versiones
    Task<IEnumerable<DocumentoVersionDto>> ObtenerVersionesAsync(int documentoId);
    Task<DocumentoVersionDto> CargarVersionAsync(int documentoId, string nombreArchivo, Stream archivoStream, int currentUserId, string ipAddress);
    Task<(Stream fileStream, string fileName, string contentType)> DescargarVersionAsync(int documentoId, int versionId, int currentUserId, string ipAddress);

    // Auditoría
    Task<IEnumerable<AuditoriaDocumentalDto>> ObtenerAuditoriaAsync(int documentoId);
    Task<IEnumerable<AuditoriaDocumentalDto>> ObtenerTodasAuditoriasAsync();

    // Fuentes de Conocimiento
    Task<IEnumerable<FuenteConocimientoDto>> ObtenerFuentesDocumentoAsync(int documentoId);
    Task AsignarFuentesDocumentoAsync(int documentoId, List<int> fuentes, int currentUserId, string ipAddress);
    Task<IEnumerable<FuenteConocimientoDto>> ObtenerFuentesConocimientoAsync();
    Task<IEnumerable<DocumentoDto>> ObtenerDocumentosDisponiblesAsync(int? idFuenteExcluir);
}
