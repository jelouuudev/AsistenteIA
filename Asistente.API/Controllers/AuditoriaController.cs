using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private readonly IAuditoriaService _auditoriaService;

    public AuditoriaController(IAuditoriaService auditoriaService)
    {
        _auditoriaService = auditoriaService;
    }

    [HttpGet("sesiones")]
    public async Task<ActionResult<IEnumerable<AuditoriaSesionDto>>> GetSesiones()
    {
        var sesiones = await _auditoriaService.ObtenerSesionesAsync();
        return Ok(sesiones);
    }

    [HttpGet("actividades")]
    public async Task<ActionResult<IEnumerable<AuditoriaActividadDto>>> GetActividades()
    {
        var actividades = await _auditoriaService.ObtenerActividadesAsync();
        return Ok(actividades);
    }

    [HttpPost("actividad")]
    public async Task<IActionResult> PostActividad([FromBody] AuditoriaActividadDto request)
    {
        await _auditoriaService.RegistrarActividadAsync(
            request.IdUsuario,
            request.Modulo,
            request.Accion,
            request.Descripcion,
            request.DireccionIP ?? HttpContext.Connection.RemoteIpAddress?.ToString()
        );
        return Ok();
    }
}
