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
public class ConsultasPlantillasController : ControllerBase
{
    private readonly IConsultaPlantillaService _plantillaService;

    public ConsultasPlantillasController(IConsultaPlantillaService plantillaService)
    {
        _plantillaService = plantillaService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConsultaPlantillaDto>>> GetAll()
    {
        var plantillas = await _plantillaService.ObtenerTodasAsync();
        return Ok(plantillas);
    }

    [HttpGet("activas")]
    public async Task<ActionResult<IEnumerable<ConsultaPlantillaDto>>> GetActivas()
    {
        var plantillas = await _plantillaService.ObtenerActivasAsync();
        return Ok(plantillas);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ConsultaPlantillaDto>> GetById(int id)
    {
        var plantilla = await _plantillaService.ObtenerPorIdAsync(id);
        if (plantilla == null) return NotFound("Plantilla no encontrada.");
        return Ok(plantilla);
    }

    [HttpPost]
    public async Task<ActionResult<ConsultaPlantillaDto>> Create([FromBody] CrearConsultaPlantillaRequest request)
    {
        try
        {
            var plantilla = await _plantillaService.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = plantilla.IdPlantilla }, plantilla);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ConsultaPlantillaDto>> Update(int id, [FromBody] ActualizarConsultaPlantillaRequest request)
    {
        try
        {
            var plantilla = await _plantillaService.ActualizarAsync(id, request);
            return Ok(plantilla);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _plantillaService.EliminarAsync(id);
            return NoContent();
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
            await _plantillaService.ActivarAsync(id);
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
            await _plantillaService.DesactivarAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
