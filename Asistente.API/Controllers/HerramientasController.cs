using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HerramientasController : ControllerBase
{
    private readonly IHerramientaService _herramientaService;

    public HerramientasController(IHerramientaService herramientaService)
    {
        _herramientaService = herramientaService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HerramientaDto>>> GetAll([FromQuery] int? idAsistente)
        => Ok(await _herramientaService.ObtenerTodasAsync(idAsistente));

    [HttpGet("{id}")]
    public async Task<ActionResult<HerramientaDto>> GetById(int id)
    {
        var h = await _herramientaService.ObtenerPorIdAsync(id);
        return h == null ? NotFound("Herramienta no encontrada.") : Ok(h);
    }

    [HttpPost]
    public async Task<ActionResult<HerramientaDto>> Create([FromBody] CrearHerramientaRequest request)
    {
        try
        {
            var creada = await _herramientaService.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = creada.IdHerramienta }, creada);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarHerramientaRequest request)
    {
        try
        {
            await _herramientaService.ActualizarAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _herramientaService.ActivarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _herramientaService.DesactivarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _herramientaService.EliminarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}
