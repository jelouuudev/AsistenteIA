using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/seguridad")]
[Authorize]
public class SeguridadController : ControllerBase
{
    private readonly IPermisoService _permisoService;
    private readonly IPoliticaIAService _politicaService;
    private readonly IDashboardSeguridadService _dashboardService;
    private readonly IUsuarioAsistenteRepository _usuarioAsistenteRepository;
    private readonly IUsuarioFuenteRepository _usuarioFuenteRepository;
    private readonly IAsistenteRepository _asistenteRepository;
    private readonly IFuenteConocimientoRepository _fuenteRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SeguridadController(
        IPermisoService permisoService,
        IPoliticaIAService politicaService,
        IDashboardSeguridadService dashboardService,
        IUsuarioAsistenteRepository usuarioAsistenteRepository,
        IUsuarioFuenteRepository usuarioFuenteRepository,
        IAsistenteRepository asistenteRepository,
        IFuenteConocimientoRepository fuenteRepository,
        IUsuarioRepository usuarioRepository,
        IUnitOfWork unitOfWork)
    {
        _permisoService = permisoService;
        _politicaService = politicaService;
        _dashboardService = dashboardService;
        _usuarioAsistenteRepository = usuarioAsistenteRepository;
        _usuarioFuenteRepository = usuarioFuenteRepository;
        _asistenteRepository = asistenteRepository;
        _fuenteRepository = fuenteRepository;
        _usuarioRepository = usuarioRepository;
        _unitOfWork = unitOfWork;
    }

    private int UsuarioId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private async Task<ActionResult?> VerificarPermisoAsync(string codigo, CancellationToken ct = default)
    {
        if (!await _permisoService.TienePermisoAsync(UsuarioId(), codigo, ct))
            return StatusCode(403, $"No tiene el permiso requerido: {codigo}.");
        return null;
    }

    // --- Permisos y matriz: solo rol Administrador (la pestaña web también).
    // CONFIGURACION_ADMINISTRAR controla únicamente la creación de políticas,
    // así quitarlo nunca bloquea esta matriz (sin riesgo de autobloqueo).
    [HttpGet("permisos")]
    public async Task<ActionResult<IEnumerable<PermisoDto>>> GetPermisos(CancellationToken ct = default)
    {
        await Task.CompletedTask;
        if (!User.IsInRole("Administrador"))
            return StatusCode(403, "Solo el rol Administrador puede ver el catálogo de permisos.");
        return Ok(await _permisoService.ObtenerTodosAsync(ct));
    }

    [HttpGet("permisos/mios")]
    public async Task<ActionResult<IEnumerable<string>>> MisPermisos(CancellationToken ct = default)
        => Ok(await _permisoService.ObtenerCodigosPorUsuarioAsync(UsuarioId(), ct));

    // --- Asignación de permisos al rol (matriz rol×permiso) ---
    [HttpGet("roles/{idRol}/permisos")]
    public async Task<ActionResult<IEnumerable<PermisoAsignadoDto>>> PermisosDeRol(int idRol, CancellationToken ct = default)
    {
        var todos = await _permisoService.ObtenerTodosAsync(ct);
        var asignados = (await _permisoService.ObtenerPorRolAsync(idRol, ct)).Select(p => p.Codigo).ToHashSet();
        return Ok(todos.Select(p => new PermisoAsignadoDto
        {
            IdPermiso = p.IdPermiso, Codigo = p.Codigo, Nombre = p.Nombre, Modulo = p.Modulo,
            Asignado = asignados.Contains(p.Codigo)
        }));
    }

    [HttpPost("roles/{idRol}/permisos")]
    public async Task<ActionResult> AsignarPermisosRol(int idRol, [FromBody] AsignarPermisosRolRequest request, CancellationToken ct = default)
    {
        if (!User.IsInRole("Administrador"))
            return StatusCode(403, "Solo el rol Administrador puede modificar la matriz de permisos.");
        await _permisoService.ReemplazarPermisosRolAsync(idRol, request.Codigos ?? new List<string>(), ct);
        return Ok("Permisos del rol actualizados.");
    }

    // --- Políticas de IA ---
    [HttpGet("politicas")]
    public async Task<ActionResult<IEnumerable<PoliticaIADto>>> GetPoliticas(CancellationToken ct = default)
        => Ok(await _politicaService.ObtenerTodasAsync(ct));

    [HttpPost("politicas")]
    public async Task<ActionResult> CrearPolitica([FromBody] CrearPoliticaIARequest request, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        if (!User.IsInRole("Administrador"))
            return StatusCode(403, "Solo el rol Administrador puede crear políticas.");
        await _politicaService.CrearAsync(request, ct);
        return Ok("Política creada.");
    }

    // --- Dashboard ---
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSeguridadDto>> Dashboard(CancellationToken ct = default)
    {
        var deny = await VerificarPermisoAsync("AUDITORIA_CONSULTAR", ct);
        if (deny != null) return deny;
        return Ok(await _dashboardService.ObtenerAsync(ct));
    }

    // --- Asignación de asistentes al usuario ---
    [HttpGet("usuarios/{idUsuario}/asistentes")]
    public async Task<ActionResult> AsistentesDeUsuario(int idUsuario, CancellationToken ct = default)
    {
        var deny = await VerificarPermisoAsync("ASISTENTES_ADMINISTRAR", ct);
        if (deny != null) return deny;
        var autorizados = await _usuarioAsistenteRepository.GetAsistentesAutorizadosAsync(idUsuario, ct);
        var todos = await _asistenteRepository.GetAllAsync();
        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario);
        var rolesUsuario = usuario?.UsuarioRoles
            .Where(ur => ur.Rol != null)
            .ToDictionary(ur => ur.IdRol, ur => ur.Rol!.Nombre) ?? new Dictionary<int, string>();
        var dto = new List<AsistenteAutorizadoDto>();
        foreach (var a in todos)
        {
            var completo = await _asistenteRepository.GetByIdAsync(a.IdAsistente);
            var porRol = (completo?.AgentesRoles ?? Enumerable.Empty<Asistente.Domain.Entities.AgenteRol>())
                .Where(ar => ar.Activo && rolesUsuario.ContainsKey(ar.IdRol))
                .Select(ar => rolesUsuario[ar.IdRol])
                .Distinct()
                .ToList();
            dto.Add(new AsistenteAutorizadoDto { IdAsistente = a.IdAsistente, Nombre = a.Nombre, Autorizado = autorizados.Contains(a.IdAsistente), RolesQueOtorgan = porRol });
        }
        return Ok(dto);
    }

    [HttpPost("usuarios/{idUsuario}/asistentes")]
    public async Task<ActionResult> AsignarAsistentes(int idUsuario, [FromBody] AsignarAsistentesUsuarioRequest request, CancellationToken ct = default)
    {
        var deny = await VerificarPermisoAsync("ASISTENTES_ADMINISTRAR", ct);
        if (deny != null) return deny;
        await _usuarioAsistenteRepository.DeleteByUsuarioAsync(idUsuario, ct);
        foreach (var id in request.IdsAsistentes)
            await _usuarioAsistenteRepository.AddAsync(new UsuarioAsistente { IdUsuario = idUsuario, IdAsistente = id, Activo = true }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok("Asistentes asignados.");
    }

    // --- Asignación de fuentes al usuario ---
    [HttpGet("usuarios/{idUsuario}/fuentes")]
    public async Task<ActionResult> FuentesDeUsuario(int idUsuario, CancellationToken ct = default)
    {
        var deny = await VerificarPermisoAsync("FUENTES_ADMINISTRAR", ct);
        if (deny != null) return deny;
        var autorizadas = await _usuarioFuenteRepository.GetFuentesAutorizadasAsync(idUsuario, ct);
        var todas = await _fuenteRepository.GetAllAsync();
        var dto = new List<FuenteAutorizadaDto>();
        foreach (var f in todas)
            dto.Add(new FuenteAutorizadaDto { IdFuente = f.IdFuente, Nombre = f.Nombre, Autorizada = autorizadas.Contains(f.IdFuente) });
        return Ok(dto);
    }

    [HttpPost("usuarios/{idUsuario}/fuentes")]
    public async Task<ActionResult> AsignarFuentes(int idUsuario, [FromBody] AsignarFuentesUsuarioRequest request, CancellationToken ct = default)
    {
        var deny = await VerificarPermisoAsync("FUENTES_ADMINISTRAR", ct);
        if (deny != null) return deny;
        await _usuarioFuenteRepository.DeleteByUsuarioAsync(idUsuario, ct);
        foreach (var id in request.IdsFuentes)
            await _usuarioFuenteRepository.AddAsync(new UsuarioFuente { IdUsuario = idUsuario, IdFuente = id, Activo = true }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok("Fuentes asignadas.");
    }
}
