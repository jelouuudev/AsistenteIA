using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IPermisoService
{
    Task<IEnumerable<PermisoDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> ObtenerCodigosPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<bool> TienePermisoAsync(int idUsuario, string codigoPermiso, CancellationToken ct = default);
    Task AsignarPermisosRolAsync(int idRol, IEnumerable<string> codigos, CancellationToken ct = default);
    Task<IEnumerable<PermisoDto>> ObtenerPorRolAsync(int idRol, CancellationToken ct = default);
    Task ReemplazarPermisosRolAsync(int idRol, IEnumerable<string> codigos, CancellationToken ct = default);
    Task CrearPermisoAsync(CrearPermisoRequest request, CancellationToken ct = default);
}

public interface IAutorizacionService
{
    Task<ResultadoAutorizacion> VerificarAsistenteAsync(int idUsuario, int idAsistente, CancellationToken ct = default);
    Task<ResultadoAutorizacion> VerificarFuenteAsync(int idUsuario, int idFuente, CancellationToken ct = default);
    Task<ResultadoAutorizacion> VerificarHerramientaAsync(int idUsuario, int idAsistente, string codigoHerramienta, CancellationToken ct = default);
    Task<ResultadoAutorizacion> VerificarPermisoAsync(int idUsuario, string codigoPermiso, CancellationToken ct = default);
    /// <summary>
    /// Verifica que el workflow esté asignado y activo para el asistente.
    /// Sin asignación no hay acceso (mundo cerrado, igual que fuentes).
    /// </summary>
    Task<ResultadoAutorizacion> VerificarWorkflowAsistenteAsync(int idAsistente, int idWorkflow, CancellationToken ct = default);
}

public interface IPoliticaIAService
{
    Task<IEnumerable<PoliticaIADto>> ObtenerTodasAsync(CancellationToken ct = default);
    Task<PoliticaIADto?> ObtenerPorTipoAsync(string tipo, CancellationToken ct = default);
    Task CrearAsync(CrearPoliticaIARequest request, CancellationToken ct = default);
    Task ActualizarAsync(int id, CrearPoliticaIARequest request, CancellationToken ct = default);
}

/// <summary>
/// Enmascara información sensible antes de enviarla al modelo (Actividad 10 / Regla 8).
/// </summary>
public interface IProteccionDatosService
{
    string Enmascarar(string texto);
    bool ContieneSensible(string texto);
}

/// <summary>
/// Protección contra Prompt Injection (Actividad 11 / Reglas 3, 5).
/// </summary>
public interface IPromptInjectionService
{
    bool EsMalicioso(string contenido, out string razon);
    string SanitizarContenidoRecuperado(string contenido);
}

/// <summary>
/// Rate Limiting por usuario/IP (Actividad 13 / Caso 5).
/// </summary>
public interface IRateLimitService
{
    (bool Permitido, int IntentosRestantes, int SegundosBloqueo) RegistrarYVerificar(string clave, int maxPorVentana, int ventanaSegundos);
}

public interface IDashboardSeguridadService
{
    Task<DashboardSeguridadDto> ObtenerAsync(CancellationToken ct = default);
}
