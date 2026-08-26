using Asistente.Application.Services;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AsistentesController : ControllerBase
{
    private readonly AsistenteService _asistenteService;

    public AsistentesController(AsistenteService asistenteService)
    {
        _asistenteService = asistenteService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AsistenteDto>>> GetAll()
    {
        var asistentes = await _asistenteService.ObtenerTodosAsync();
        return Ok(asistentes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AsistenteDto>> GetById(int id)
    {
        var asistente = await _asistenteService.ObtenerPorIdAsync(id);
        if (asistente == null) return NotFound("Asistente no encontrado.");
        return Ok(asistente);
    }

    [HttpPost]
    public async Task<ActionResult<AsistenteDto>> Create([FromBody] CrearAsistenteRequest request)
    {
        try
        {
            var asistente = await _asistenteService.CrearAsistenteAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = asistente.IdAsistente }, asistente);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AsistenteDto>> Update(int id, [FromBody] ActualizarAsistenteRequest request)
    {
        try
        {
            var asistente = await _asistenteService.ActualizarAsistenteAsync(id, request);
            return Ok(asistente);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activate(int id)
    {
        try
        {
            await _asistenteService.ActivarAsistenteAsync(id);
            return NoContent();
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
            await _asistenteService.DesactivarAsistenteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
