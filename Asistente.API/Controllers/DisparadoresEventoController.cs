using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/disparadores-evento")]
[Authorize(Roles = "Administrador")]
public class DisparadoresEventoController : ControllerBase
{
    private readonly IDisparadorEventoService _service;
    public DisparadoresEventoController(IDisparadorEventoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DisparadorEventoDto>>> GetAll(CancellationToken ct)
    {
        return Ok(await _service.ObtenerTodosAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DisparadorEventoDto>> GetById(int id, CancellationToken ct)
    {
        var result = await _service.ObtenerPorIdAsync(id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DisparadorEventoDto>> Crear([FromBody] CrearDisparadorEventoRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.CrearAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.IdDisparador }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarDisparadorEventoRequest request, CancellationToken ct)
    {
        try
        {
            await _service.ActualizarAsync(id, request, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        try
        {
            await _service.EliminarAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:int}/activar")]
    public async Task<IActionResult> Activar(int id, CancellationToken ct)
    {
        try
        {
            await _service.CambiarEstadoAsync(id, true, ct);
            return Ok(new { activo = true });
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        try
        {
            await _service.CambiarEstadoAsync(id, false, ct);
            return Ok(new { activo = false });
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}
