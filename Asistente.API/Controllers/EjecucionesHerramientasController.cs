using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EjecucionesHerramientasController : ControllerBase
{
    private readonly IEjecucionHerramientaService _ejecucionService;

    public EjecucionesHerramientasController(IEjecucionHerramientaService ejecucionService)
    {
        _ejecucionService = ejecucionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EjecucionHerramientaDto>>> GetAll()
        => Ok(await _ejecucionService.ObtenerTodasAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<EjecucionHerramientaDto>> GetById(int id)
    {
        var e = await _ejecucionService.ObtenerPorIdAsync(id);
        return e == null ? NotFound("Ejecución no encontrada.") : Ok(e);
    }
}
