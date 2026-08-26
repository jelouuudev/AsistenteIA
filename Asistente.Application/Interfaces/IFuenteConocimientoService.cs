using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IFuenteConocimientoService
{
    Task<IEnumerable<FuenteConocimientoDto>> ObtenerTodasAsync();
    Task<IEnumerable<FuenteConocimientoDto>> ObtenerActivasAsync();
    Task<FuenteConocimientoDto?> ObtenerPorIdAsync(int id);
    Task<FuenteConocimientoDto> CrearAsync(CrearFuenteConocimientoRequest request, int usuarioCreacion);
    Task<FuenteConocimientoDto> ActualizarAsync(int id, ActualizarFuenteConocimientoRequest request);
    Task ActivarAsync(int id);
    Task DesactivarAsync(int id);
    Task<DashboardFuentesDto> ObtenerDashboardAsync();

    Task<IEnumerable<AsistenteFuenteDto>> ObtenerFuentesDeAsistenteAsync(int idAsistente);
    Task<IEnumerable<AsistenteFuenteDto>> ObtenerAsistentesDeFuenteAsync(int idFuente);
    Task AsignarFuenteAAsistenteAsync(AsignarFuenteAAsistenteRequest request);
    Task DesasignarFuenteDeAsistenteAsync(int idAsistente, int idFuente);
    Task ActivarAsistenteFuenteAsync(int idAsistente, int idFuente);
    Task DesactivarAsistenteFuenteAsync(int idAsistente, int idFuente);

    Task<IEnumerable<DocumentoFuenteDto>> ObtenerDocumentosDeFuenteAsync(int idFuente);
    Task<IEnumerable<FuenteConocimientoDto>> ObtenerFuentesDeDocumentoAsync(int idDocumento);
    Task AsignarDocumentoAFuenteAsync(AsignarDocumentoAFuenteRequest request);
    Task DesasignarDocumentoDeFuenteAsync(int idDocumento, int idFuente);
}
