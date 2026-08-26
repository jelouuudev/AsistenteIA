using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/configuracionrag")]
[Authorize(Roles = "Administrador")]
public class ConfiguracionRAGController : ControllerBase
{
    private readonly IConfiguracionRAGService _service;

    public ConfiguracionRAGController(IConfiguracionRAGService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracionRAGDto>> Obtener()
    {
        var config = await _service.ObtenerActivaAsync();
        return Ok(config);
    }

    [HttpPut]
    public async Task<ActionResult<ConfiguracionRAGDto>> Actualizar([FromBody] ActualizarConfiguracionRAGRequest request)
    {
        var config = await _service.ActualizarAsync(request);
        return Ok(config);
    }
}
