using System.Security.Claims;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MemoriaController : ControllerBase
{
    private readonly IMemoriaService _memoriaService;

    public MemoriaController(IMemoriaService memoriaService)
    {
        _memoriaService = memoriaService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return -1;
        return userId;
    }

    [HttpGet("conversaciones")]
    public async Task<ActionResult<IEnumerable<ConversacionListDto>>> ObtenerConversaciones()
    {
        var userId = GetCurrentUserId();
        if (userId == -1)
            return Unauthorized(new { error = "Usuario no autenticado." });

        var conversaciones = await _memoriaService.ObtenerConversacionesAsync(userId);
        return Ok(conversaciones);
    }

    [HttpGet("conversaciones/buscar")]
    public async Task<ActionResult<IEnumerable<ConversacionListDto>>> BuscarConversaciones([FromQuery] string q)
    {
        var userId = GetCurrentUserId();
        if (userId == -1)
            return Unauthorized(new { error = "Usuario no autenticado." });

        if (string.IsNullOrWhiteSpace(q))
            return Ok(Enumerable.Empty<ConversacionListDto>());

        var conversaciones = await _memoriaService.BuscarConversacionesAsync(userId, q);
        return Ok(conversaciones);
    }

    [HttpGet("conversaciones/{id}")]
    public async Task<ActionResult<ConversacionDto>> ObtenerConversacion(int id)
    {
        var conversacion = await _memoriaService.ObtenerConversacionAsync(id);
        if (conversacion == null)
            return NotFound();

        return Ok(conversacion);
    }

    [HttpPost("conversaciones")]
    public async Task<ActionResult<ConversacionDto>> CrearConversacion()
    {
        var userId = GetCurrentUserId();
        if (userId == -1)
            return Unauthorized(new { error = "Usuario no autenticado." });

        var conversacion = await _memoriaService.CrearConversacionAsync(userId);
        return Ok(conversacion);
    }

    [HttpPut("conversaciones/{id}/renombrar")]
    public async Task<IActionResult> RenombrarConversacion(int id, [FromBody] RenombrarConversacionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo))
            return BadRequest(new { error = "El título no puede estar vacío." });

        try
        {
            await _memoriaService.RenombrarConversacionAsync(id, request.Titulo);
            return Ok(new { exitoso = true });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("conversaciones/{id}/archivar")]
    public async Task<IActionResult> ArchivarConversacion(int id)
    {
        try
        {
            await _memoriaService.ArchivarConversacionAsync(id);
            return Ok(new { exitoso = true });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("conversaciones/{id}/eliminar")]
    public async Task<IActionResult> EliminarConversacion(int id)
    {
        try
        {
            await _memoriaService.EliminarConversacionAsync(id);
            return Ok(new { exitoso = true });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("conversaciones/{id}/debug")]
    public async Task<ActionResult<DebugContextoDto>> ObtenerDebug(int id)
    {
        try
        {
            var debug = await _memoriaService.ObtenerDebugContextoAsync(id);
            return Ok(debug);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
