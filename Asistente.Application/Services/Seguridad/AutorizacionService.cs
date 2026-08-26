using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Seguridad;

/// <summary>
/// Autorización en cada capa (ETAPA 14 - Actividades 4, 5, 6, 7 y Reglas 2, 4, 5).
/// Valida asistentes, fuentes, herramientas y workflows autorizados por usuario.
/// El administrador (Rol "Administrador") tiene acceso total por defecto.
/// </summary>
public class AutorizacionService : IAutorizacionService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUsuarioAsistenteRepository _usuarioAsistenteRepository;
    private readonly IUsuarioFuenteRepository _usuarioFuenteRepository;
    private readonly IPermisoRepository _permisoRepository;
    private readonly IAsistenteRepository _asistenteRepository;

    public AutorizacionService(
        IUsuarioRepository usuarioRepository,
        IUsuarioAsistenteRepository usuarioAsistenteRepository,
        IUsuarioFuenteRepository usuarioFuenteRepository,
        IPermisoRepository permisoRepository,
        IAsistenteRepository asistenteRepository)
    {
        _usuarioRepository = usuarioRepository;
        _usuarioAsistenteRepository = usuarioAsistenteRepository;
        _usuarioFuenteRepository = usuarioFuenteRepository;
        _permisoRepository = permisoRepository;
        _asistenteRepository = asistenteRepository;
    }

    private async Task<bool> EsAdministradorAsync(int idUsuario, CancellationToken ct)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario);
        return usuario?.UsuarioRoles.Any(ur => ur.Rol?.Nombre == "Administrador") ?? false;
    }

    public async Task<ResultadoAutorizacion> VerificarAsistenteAsync(int idUsuario, int idAsistente, CancellationToken ct = default)
    {
        if (await EsAdministradorAsync(idUsuario, ct))
            return Ok();

        // Agente asignado directamente al usuario (ETAPA 14)
        if (await _usuarioAsistenteRepository.EstaAutorizadoAsync(idUsuario, idAsistente, ct))
            return Ok();

        // ETAPA 16 - Regla 1: agente asignado a alguno de los roles del usuario
        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario);
        if (usuario != null)
        {
            var rolesUsuario = usuario.UsuarioRoles
                .Where(ur => ur.Rol != null)
                .Select(ur => ur.Rol.IdRol)
                .ToList();
            if (rolesUsuario.Any())
            {
                var agente = await _asistenteRepository.GetByIdAsync(idAsistente);
                if (agente != null && agente.AgentesRoles
                        .Any(ar => ar.Activo && rolesUsuario.Contains(ar.IdRol)))
                    return Ok();
            }
        }

        return Denegado("El agente no está autorizado para este usuario.");
    }

    public async Task<ResultadoAutorizacion> VerificarFuenteAsync(int idUsuario, int idFuente, CancellationToken ct = default)
    {
        if (await EsAdministradorAsync(idUsuario, ct))
            return Ok();
        var autorizada = await _usuarioFuenteRepository.EstaAutorizadaAsync(idUsuario, idFuente, ct);
        return autorizada ? Ok() : Denegado("La fuente de conocimiento no está autorizada para este usuario.");
    }

    public async Task<ResultadoAutorizacion> VerificarHerramientaAsync(int idUsuario, int idAsistente, string codigoHerramienta, CancellationToken ct = default)
    {
        if (await EsAdministradorAsync(idUsuario, ct))
            return Ok();

        // El usuario debe tener el permiso asociado a la herramienta (Caso 1 y 3).
        // La asociación herramienta->asistente ya la valida el ToolOrchestrator.
        var codigoPermiso = CodigoPermisoParaHerramienta(codigoHerramienta);
        if (!string.IsNullOrEmpty(codigoPermiso))
        {
            var tiene = await _permisoRepository.ObtenerCodigosPorUsuarioAsync(idUsuario, ct);
            if (!tiene.Contains(codigoPermiso))
                return Denegado($"El usuario no tiene el permiso requerido ({codigoPermiso}) para usar la herramienta '{codigoHerramienta}'.");
        }
        return Ok();
    }

    public async Task<ResultadoAutorizacion> VerificarPermisoAsync(int idUsuario, string codigoPermiso, CancellationToken ct = default)
    {
        if (await EsAdministradorAsync(idUsuario, ct))
            return Ok();
        var tiene = await _permisoRepository.ObtenerCodigosPorUsuarioAsync(idUsuario, ct);
        return tiene.Contains(codigoPermiso) ? Ok() : Denegado($"Permiso requerido: {codigoPermiso}.");
    }

    /// <summary>
    /// Mapea una herramienta a su permiso (Actividad 6 / Casos 1 y 3).
    /// </summary>
    public static string? CodigoPermisoParaHerramienta(string codigoHerramienta) => codigoHerramienta switch
    {
        "SqlQueryTool" => "SQL_CONSULTAR",
        "ReportTool" => "HERRAMIENTAS_ADMINISTRAR",
        "DocumentSearchTool" => "HERRAMIENTAS_CONSULTAR",
        "CalculatorTool" => "HERRAMIENTAS_CONSULTAR",
        "DateTimeTool" => "HERRAMIENTAS_CONSULTAR",
        _ => "HERRAMIENTAS_CONSULTAR"
    };

    private static ResultadoAutorizacion Ok() => new() { Permitido = true };
    private static ResultadoAutorizacion Denegado(string motivo) => new() { Permitido = false, Motivo = motivo };
}
