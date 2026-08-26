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
public class ProcesamientoController : ControllerBase
{
    private readonly IProcesamientoDocumentalService _procesamientoService;

    public ProcesamientoController(IProcesamientoDocumentalService procesamientoService)
    {
        _procesamientoService = procesamientoService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentoProcesadoDto>>> GetAll()
    {
        var resultado = await _procesamientoService.ObtenerTodosAsync();
        return Ok(resultado);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardProcesamientoDto>> GetDashboard()
    {
        var dashboard = await _procesamientoService.ObtenerDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentoProcesadoDto>> GetById(int id)
    {
        var resultado = await _procesamientoService.ObtenerPorIdAsync(id);
        if (resultado == null) return NotFound("Registro de procesamiento no encontrado.");
        return Ok(resultado);
    }

    [HttpGet("estado/{estado}")]
    public async Task<ActionResult<IEnumerable<DocumentoProcesadoDto>>> GetByEstado(string estado)
    {
        var resultado = await _procesamientoService.ObtenerPorEstadoAsync(estado);
        return Ok(resultado);
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<DocumentoProcesadoDto>> GetByVersionId(int versionId)
    {
        var resultado = await _procesamientoService.ObtenerPorVersionIdAsync(versionId);
        if (resultado == null) return NotFound("No se encontró procesamiento para esta versión.");
        return Ok(resultado);
    }

    [HttpGet("{id}/chunks")]
    public async Task<ActionResult<IEnumerable<DocumentoChunkDto>>> GetChunks(int id)
    {
        var chunks = await _procesamientoService.ObtenerChunksAsync(id);
        return Ok(chunks);
    }

    [HttpPost("procesar/{versionId}")]
    public async Task<IActionResult> Procesar(int versionId)
    {
        try
        {
            await _procesamientoService.ProcesarDocumentoAsync(versionId);
            return Ok(new { mensaje = "Documento procesado exitosamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al procesar: {ex.Message}");
        }
    }

    [HttpPost("reprocesar/{id}")]
    public async Task<IActionResult> Reprocesar(int id)
    {
        try
        {
            await _procesamientoService.ReprocesarDocumentoAsync(id);
            return Ok(new { mensaje = "Documento reprocesado exitosamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al reprocesar: {ex.Message}");
        }
    }

    [HttpGet("pendientes")]
    public async Task<ActionResult<IEnumerable<DocumentoProcesadoDto>>> GetPendientes()
    {
        var resultado = await _procesamientoService.ObtenerDocumentosPendientesAsync();
        return Ok(resultado);
    }

    [HttpPost("reparar-chunks")]
    public async Task<IActionResult> RepararChunks()
    {
        try
        {
            var reparados = await _procesamientoService.RepararDocumentosConChunksFaltantesAsync();
            return Ok(new { mensaje = $"Reparación completada. {reparados} documentos reprocesados." });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al reparar: {ex.Message}");
        }
    }
}
