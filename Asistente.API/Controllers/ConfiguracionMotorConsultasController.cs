using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public class ConfiguracionMotorConsultasController : ControllerBase
{
    private readonly IConfiguracionMotorConsultasService _configService;

    public ConfiguracionMotorConsultasController(IConfiguracionMotorConsultasService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracionMotorConsultasDto>> GetActiva()
    {
        var config = await _configService.ObtenerActivaAsync();
        if (config == null) return NotFound("No existe configuración activa para el motor de consultas.");
        return Ok(config);
    }

    [HttpPut]
    public async Task<ActionResult<ConfiguracionMotorConsultasDto>> Update([FromBody] ActualizarConfiguracionMotorConsultasRequest request)
    {
        var config = await _configService.ActualizarAsync(request);
        return Ok(config);
    }
}
