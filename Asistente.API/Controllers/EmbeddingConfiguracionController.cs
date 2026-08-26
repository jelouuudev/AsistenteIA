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
public class EmbeddingConfiguracionController : ControllerBase
{
    private readonly IEmbeddingConfiguracionService _configService;

    public EmbeddingConfiguracionController(IEmbeddingConfiguracionService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmbeddingConfiguracionDto>>> GetAll()
    {
        var resultado = await _configService.ObtenerTodasAsync();
        return Ok(resultado);
    }

    [HttpGet("activa")]
    public async Task<ActionResult<EmbeddingConfiguracionDto>> GetActiva()
    {
        var resultado = await _configService.ObtenerActivaAsync();
        if (resultado == null) return NotFound("No hay configuración activa de embeddings.");
        return Ok(resultado);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EmbeddingConfiguracionDto>> GetById(int id)
    {
        var resultado = await _configService.ObtenerPorIdAsync(id);
        if (resultado == null) return NotFound("Configuración no encontrada.");
        return Ok(resultado);
    }

    [HttpPost]
    public async Task<ActionResult<EmbeddingConfiguracionDto>> Crear(
        [FromBody] ActualizarEmbeddingConfiguracionRequest request)
    {
        try
        {
            var resultado = await _configService.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = resultado.IdConfiguracion }, resultado);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al crear configuración: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EmbeddingConfiguracionDto>> Actualizar(
        int id,
        [FromBody] ActualizarEmbeddingConfiguracionRequest request)
    {
        try
        {
            var resultado = await _configService.ActualizarAsync(id, request);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al actualizar configuración: {ex.Message}");
        }
    }
}
