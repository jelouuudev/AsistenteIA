using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public AuthController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (request == null)
        {
            return BadRequest(new LoginResponse { Exitoso = false, Error = "Petición inválida." });
        }

        // Set IP and browser details if not filled by caller
        if (string.IsNullOrEmpty(request.DireccionIP))
        {
            request.DireccionIP = Request.Headers.TryGetValue("X-User-IP", out var ip) 
                ? ip.ToString() 
                : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }
        if (string.IsNullOrEmpty(request.Navegador))
        {
            request.Navegador = Request.Headers["User-Agent"].ToString();
        }

        var response = await _usuarioService.LoginAsync(request);
        if (!response.Exitoso)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Token inválido." });
        }

        var usuario = await _usuarioService.ObtenerPorIdAsync(userId);
        if (usuario == null)
        {
            return NotFound(new { error = "Usuario no encontrado." });
        }

        return Ok(usuario);
    }

    [HttpPost("logout/{sessionId}")]
    public async Task<IActionResult> Logout(int sessionId)
    {
        await _usuarioService.LogoutAsync(sessionId);
        return Ok();
    }
}
