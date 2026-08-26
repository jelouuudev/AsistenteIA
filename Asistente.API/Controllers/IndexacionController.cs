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
public class IndexacionController : ControllerBase
{
    private readonly IIndexacionService _indexacionService;

    public IndexacionController(IIndexacionService indexacionService)
    {
        _indexacionService = indexacionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentoIndexadoDto>>> GetAll()
    {
        var resultado = await _indexacionService.ObtenerTodosAsync();
        return Ok(resultado);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardIndexacionDto>> GetDashboard()
    {
        var dashboard = await _indexacionService.ObtenerDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentoIndexadoDto>> GetById(int id)
    {
        var resultado = await _indexacionService.ObtenerPorIdAsync(id);
        if (resultado == null) return NotFound("Registro de indexación no encontrado.");
        return Ok(resultado);
    }

    [HttpGet("procesado/{procesadoId}")]
    public async Task<ActionResult<DocumentoIndexadoDto>> GetByProcesadoId(int procesadoId)
    {
        var resultado = await _indexacionService.ObtenerPorProcesadoIdAsync(procesadoId);
        if (resultado == null) return NotFound("No se encontró indexación para este documento procesado.");
        return Ok(resultado);
    }

    [HttpGet("estado/{estado}")]
    public async Task<ActionResult<IEnumerable<DocumentoIndexadoDto>>> GetByEstado(string estado)
    {
        var resultado = await _indexacionService.ObtenerPorEstadoAsync(estado);
        return Ok(resultado);
    }

    [HttpPost("indexar/{documentoProcesadoId}")]
    public async Task<IActionResult> Indexar(int documentoProcesadoId)
    {
        try
        {
            await _indexacionService.IndexarDocumentoAsync(documentoProcesadoId);
            return Ok(new { mensaje = "Documento indexado exitosamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al indexar: {ex.Message}");
        }
    }

    [HttpPost("reindexar/{documentoProcesadoId}")]
    public async Task<IActionResult> Reindexar(int documentoProcesadoId)
    {
        try
        {
            await _indexacionService.ReindexarDocumentoAsync(documentoProcesadoId);
            return Ok(new { mensaje = "Documento reindexado exitosamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al reindexar: {ex.Message}");
        }
    }

    [HttpPost("reindexar-categoria/{categoriaId}")]
    public async Task<IActionResult> ReindexarCategoria(int categoriaId)
    {
        try
        {
            await _indexacionService.ReindexarCategoriaAsync(categoriaId);
            return Ok(new { mensaje = "Categoría reindexada exitosamente." });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al reindexar categoría: {ex.Message}");
        }
    }

    [HttpPost("reindexar-todos")]
    public async Task<IActionResult> ReindexarTodos()
    {
        try
        {
            await _indexacionService.ReindexarTodosAsync();
            return Ok(new { mensaje = "Todos los documentos reindexados exitosamente." });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al reindexar: {ex.Message}");
        }
    }

    [HttpDelete("eliminar/{documentoProcesadoId}")]
    public async Task<IActionResult> Eliminar(int documentoProcesadoId)
    {
        try
        {
            await _indexacionService.EliminarIndiceAsync(documentoProcesadoId);
            return Ok(new { mensaje = "Índice eliminado exitosamente." });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al eliminar índice: {ex.Message}");
        }
    }

    [HttpDelete("limpiar-vectorstore")]
    public async Task<IActionResult> LimpiarVectorStore()
    {
        try
        {
            await _indexacionService.LimpiarVectorStoreAsync();
            return Ok(new { mensaje = "Vectorstore limpiado exitosamente." });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al limpiar vectorstore: {ex.Message}");
        }
    }
}
