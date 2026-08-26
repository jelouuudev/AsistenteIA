using System;
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
public class ConsultasEmpresarialesController : ControllerBase
{
    private readonly IQueryEmpresarialService _queryService;

    public ConsultasEmpresarialesController(IQueryEmpresarialService queryService)
    {
        _queryService = queryService;
    }

    [HttpPost("procesar")]
    public async Task<ActionResult<ResultadoProcesarPreguntaDto>> ProcesarPregunta([FromBody] ProcesarPreguntaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
            return BadRequest("La pregunta no puede estar vacía.");

        var userId = ObtenerUserId();
        var resultado = await _queryService.ProcesarPreguntaAsync(request.Pregunta, userId);
        return Ok(resultado);
    }

    [HttpPost("ejecutar")]
    public async Task<ActionResult<EjecutarConsultaResponse>> EjecutarConsulta([FromBody] EjecutarConsultaRequest request)
    {
        var userId = ObtenerUserId();
        var resultado = await _queryService.EjecutarConsultaAsync(request, userId);
        return Ok(resultado);
    }

    private int ObtenerUserId()
    {
        var userId = 1;
        if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id))
            userId = id;
        return userId;
    }
}
