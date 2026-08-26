using Asistente.Application.Services;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/agentes")]
[Authorize]
public class AgentesController : ControllerBase
{
    private readonly AsistenteService _agenteManager;
    private readonly IUsuarioRepository _usuarioRepository;

    public AgentesController(AsistenteService agenteManager, IUsuarioRepository usuarioRepository)
    {
        _agenteManager = agenteManager;
        _usuarioRepository = usuarioRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AsistenteDto>>> GetAll()
        => Ok(await _agenteManager.ObtenerTodosAsync());

    // ETAPA 16 - Regla 1: agentes autorizados para un usuario (por rol o asignación directa).
    [HttpGet("autorizados/{idUsuario}")]
    public async Task<ActionResult<IEnumerable<AsistenteDto>>> GetAutorizados(int idUsuario)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(idUsuario);
        var roles = usuario?.UsuarioRoles
            .Where(ur => ur.Rol != null)
            .Select(ur => ur.Rol.IdRol)
            .ToList() ?? new List<int>();
        return Ok(await _agenteManager.ObtenerAutorizadosParaUsuarioAsync(idUsuario, roles));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AsistenteDto>> GetById(int id)
    {
        var a = await _agenteManager.ObtenerPorIdAsync(id);
        return a == null ? NotFound() : Ok(a);
    }

    [HttpPost]
    public async Task<ActionResult<AsistenteDto>> Crear([FromBody] CrearAsistenteRequest request)
        => Ok(await _agenteManager.CrearAsistenteAsync(request));

    [HttpPut("{id}")]
    public async Task<ActionResult<AsistenteDto>> Actualizar(int id, [FromBody] ActualizarAsistenteRequest request)
        => Ok(await _agenteManager.ActualizarAsistenteAsync(id, request));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        // Eliminación lógica (Regla 10): se desactiva en lugar de borrar físicamente.
        await _agenteManager.DesactivarAsistenteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        await _agenteManager.ActivarAsistenteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        await _agenteManager.DesactivarAsistenteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/publicar")]
    public async Task<IActionResult> Publicar(int id)
    {
        await _agenteManager.PublicarAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/enviar-prueba")]
    public async Task<IActionResult> EnviarAPrueba(int id)
    {
        await _agenteManager.EnviarAPruebaAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/duplicar")]
    public async Task<ActionResult<AsistenteDto>> Duplicar(int id)
        => Ok(await _agenteManager.DuplicarAsync(id));

    [HttpPost("{id}/versiones")]
    public async Task<ActionResult<AgenteVersionDto>> CrearVersion(int id)
        => Ok(await _agenteManager.CrearVersionAsync(id));

    [HttpGet("{id}/versiones")]
    public async Task<ActionResult<IEnumerable<AgenteVersionDto>>> Versiones(int id)
        => Ok(await _agenteManager.ObtenerVersionesAsync(id));
}
