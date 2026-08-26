using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrchestratorConfigController : ControllerBase
{
    private readonly IHerramientaService _herramientaService;

    public OrchestratorConfigController(IHerramientaService herramientaService)
    {
        _herramientaService = herramientaService;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracionOrchestratorDto>> Get()
        => Ok(await _herramientaService.ObtenerConfiguracionAsync());

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ConfiguracionOrchestratorDto config)
    {
        try
        {
            await _herramientaService.GuardarConfiguracionAsync(config);
            return NoContent();
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}
