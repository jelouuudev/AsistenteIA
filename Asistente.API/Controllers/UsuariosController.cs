using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UsuarioDto>>> GetAll()
    {
        var users = await _usuarioService.ObtenerTodosAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UsuarioDto>> GetById(int id)
    {
        var user = await _usuarioService.ObtenerPorIdAsync(id);
        if (user == null) return NotFound("Usuario no encontrado.");
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Create([FromBody] CrearUsuarioRequest request)
    {
        try
        {
            var user = await _usuarioService.CrearUsuarioAsync(request, GetCurrentUserId(), GetIpAddress());
            return CreatedAtAction(nameof(GetById), new { id = user.IdUsuario }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UsuarioDto>> Update(int id, [FromBody] ActualizarUsuarioRequest request)
    {
        try
        {
            var user = await _usuarioService.ActualizarUsuarioAsync(id, request, GetCurrentUserId(), GetIpAddress());
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await _usuarioService.DesactivarUsuarioAsync(id, GetCurrentUserId(), GetIpAddress());
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/cambiar-contrasena")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] CambiarPasswordRequest request)
    {
        try
        {
            await _usuarioService.CambiarPasswordAsync(id, request, GetCurrentUserId(), GetIpAddress());
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var id))
        {
            return id;
        }
        return 1; // Default to admin / system user
    }

    private string GetIpAddress()
    {
        if (Request.Headers.TryGetValue("X-User-IP", out var ip))
        {
            return ip.ToString();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}
