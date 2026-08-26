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
public class CategoriasDocumentoController : ControllerBase
{
    private readonly ICategoriaDocumentoService _categoriaService;

    public CategoriasDocumentoController(ICategoriaDocumentoService categoriaService)
    {
        _categoriaService = categoriaService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoriaDocumentoDto>>> GetAll()
    {
        var categorias = await _categoriaService.ObtenerTodasAsync();
        return Ok(categorias);
    }

    [HttpGet("activas")]
    public async Task<ActionResult<IEnumerable<CategoriaDocumentoDto>>> GetActivas()
    {
        var categorias = await _categoriaService.ObtenerActivasAsync();
        return Ok(categorias);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoriaDocumentoDto>> GetById(int id)
    {
        var categoria = await _categoriaService.ObtenerPorIdAsync(id);
        if (categoria == null) return NotFound("Categoría no encontrada.");
        return Ok(categoria);
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaDocumentoDto>> Create([FromBody] CrearCategoriaDocumentoRequest request)
    {
        try
        {
            var categoria = await _categoriaService.CrearAsync(request, GetCurrentUserId(), GetIpAddress());
            return CreatedAtAction(nameof(GetById), new { id = categoria.IdCategoria }, categoria);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoriaDocumentoDto>> Update(int id, [FromBody] ActualizarCategoriaDocumentoRequest request)
    {
        try
        {
            var categoria = await _categoriaService.ActualizarAsync(id, request, GetCurrentUserId(), GetIpAddress());
            return Ok(categoria);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private int GetCurrentUserId()
    {
        if (Request.Headers.TryGetValue("X-User-Id", out var val) && int.TryParse(val, out var id))
        {
            return id;
        }
        return 1;
    }

    private string GetIpAddress()
    {
        if (Request.Headers.TryGetValue("X-User-IP", out var ip))
        {
            return ip.ToString();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}
