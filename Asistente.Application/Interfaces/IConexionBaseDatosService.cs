using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IConexionBaseDatosService
{
    Task<IEnumerable<ConexionBaseDatosDto>> ObtenerTodasAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ConexionBaseDatosDto>> ObtenerActivasAsync(CancellationToken cancellationToken = default);
    Task<ConexionBaseDatosDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ConexionBaseDatosDto> CrearAsync(CrearConexionBaseDatosRequest request, CancellationToken cancellationToken = default);
    Task<ConexionBaseDatosDto> ActualizarAsync(int id, ActualizarConexionBaseDatosRequest request, CancellationToken cancellationToken = default);
    Task ActivarAsync(int id, CancellationToken cancellationToken = default);
    Task DesactivarAsync(int id, CancellationToken cancellationToken = default);
    Task EliminarAsync(int id, CancellationToken cancellationToken = default);
    Task<ConexionPruebaResultadoDto> ProbarConexionAsync(ProbarConexionRequest request, CancellationToken cancellationToken = default);
    Task<EsquemaBaseDatosDto> DescubrirEsquemaAsync(int idConexion, CancellationToken cancellationToken = default);
    Task<IEnumerable<TablaAutorizadaDto>> ObtenerTablasAutorizadasAsync(int idConexion, CancellationToken cancellationToken = default);
    Task<IEnumerable<VistaAutorizadaDto>> ObtenerVistasAutorizadasAsync(int idConexion, CancellationToken cancellationToken = default);
    Task AgregarTablaAutorizadaAsync(int idConexion, TablaAutorizadaRequest request, CancellationToken cancellationToken = default);
    Task AgregarVistaAutorizadaAsync(int idConexion, VistaAutorizadaRequest request, CancellationToken cancellationToken = default);
    Task EliminarTablaAutorizadaAsync(int idConexion, int idTabla, CancellationToken cancellationToken = default);
    Task EliminarVistaAutorizadaAsync(int idConexion, int idVista, CancellationToken cancellationToken = default);
    Task<string> ConstruirCadenaConexionAsync(CrearConexionBaseDatosRequest request);
}
