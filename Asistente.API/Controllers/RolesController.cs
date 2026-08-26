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
public class RolesController : ControllerBase
{
    private readonly IRolService _rolService;

    public RolesController(IRolService rolService)
    {
        _rolService = rolService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RolDto>>> GetAll()
    {
        var roles = await _rolService.ObtenerTodosAsync();
        return Ok(roles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RolDto>> GetById(int id)
    {
        var rol = await _rolService.ObtenerPorIdAsync(id);
        if (rol == null) return NotFound("Rol no encontrado.");
        return Ok(rol);
    }

    [HttpPost]
    public async Task<ActionResult<RolDto>> Create([FromBody] CrearRolRequest request)
    {
        try
        {
            var rol = await _rolService.CrearRolAsync(request, GetCurrentUserId(), GetIpAddress());
            return CreatedAtAction(nameof(GetById), new { id = rol.IdRol }, rol);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<RolDto>> Update(int id, [FromBody] ActualizarRolRequest request)
    {
        try
        {
            var rol = await _rolService.ActualizarRolAsync(id, request, GetCurrentUserId(), GetIpAddress());
            return Ok(rol);
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
        return 1;
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
