using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AsistenteHerramientasController : ControllerBase
{
    private readonly IHerramientaService _herramientaService;

    public AsistenteHerramientasController(IHerramientaService herramientaService)
    {
        _herramientaService = herramientaService;
    }

    [HttpGet("{idAsistente}/herramientas")]
    public async Task<ActionResult<IEnumerable<HerramientaDto>>> GetByAsistente(int idAsistente)
        => Ok(await _herramientaService.ObtenerAsociadasAlAsistenteAsync(idAsistente));

    [HttpPost("{idAsistente}/herramientas/{idHerramienta}")]
    public async Task<IActionResult> Asociar(int idAsistente, int idHerramienta, [FromQuery] bool activa = true)
    {
        try
        {
            await _herramientaService.AsociarHerramientaAsync(idAsistente, idHerramienta, activa);
            return NoContent();
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{idAsistente}/herramientas/{idHerramienta}")]
    public async Task<IActionResult> Desasociar(int idAsistente, int idHerramienta)
    {
        try
        {
            await _herramientaService.DesasociarHerramientaAsync(idAsistente, idHerramienta);
            return NoContent();
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}
