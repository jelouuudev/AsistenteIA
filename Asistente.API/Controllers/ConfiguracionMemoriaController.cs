using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracionMemoriaController : ControllerBase
{
    private readonly IConfiguracionMemoriaService _configuracionMemoriaService;

    public ConfiguracionMemoriaController(IConfiguracionMemoriaService configuracionMemoriaService)
    {
        _configuracionMemoriaService = configuracionMemoriaService;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracionMemoriaDto>> ObtenerConfiguracion()
    {
        var config = await _configuracionMemoriaService.ObtenerActivaAsync();
        if (config == null)
            return Ok(new ConfiguracionMemoriaDto
            {
                MaximoMensajesContexto = 20,
                MaximoTokensContexto = 4096,
                LongitudResumen = 500,
                CantidadConversacionesVisibles = 50,
                Activo = true
            });

        return Ok(config);
    }

    [HttpPut]
    public async Task<ActionResult<ConfiguracionMemoriaDto>> ActualizarConfiguracion(
        [FromBody] ActualizarConfiguracionMemoriaRequest request)
    {
        var config = await _configuracionMemoriaService.ActualizarAsync(request);
        return Ok(config);
    }
}
