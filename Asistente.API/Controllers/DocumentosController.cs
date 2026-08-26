using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentosController : ControllerBase
{
    private readonly IDocumentoService _documentoService;

    public DocumentosController(IDocumentoService documentoService)
    {
        _documentoService = documentoService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentoDto>>> GetFiltered([FromQuery] FiltroDocumentoRequest request)
    {
        var documentos = await _documentoService.ObtenerFiltradosAsync(request);
        return Ok(documentos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentoDto>> GetById(int id)
    {
        var documento = await _documentoService.ObtenerPorIdAsync(id);
        if (documento == null) return NotFound("Documento no encontrado.");
        return Ok(documento);
    }

    [HttpPost]
    public async Task<ActionResult<DocumentoDto>> Create([FromBody] CrearDocumentoRequest request)
    {
        try
        {
            var documento = await _documentoService.CrearAsync(request, GetCurrentUserId(), GetIpAddress());
            return CreatedAtAction(nameof(GetById), new { id = documento.IdDocumento }, documento);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DocumentoDto>> Update(int id, [FromBody] ActualizarDocumentoRequest request)
    {
        try
        {
            var documento = await _documentoService.ActualizarAsync(id, request, GetCurrentUserId(), GetIpAddress());
            return Ok(documento);
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

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        try
        {
            await _documentoService.ActivarAsync(id, GetCurrentUserId(), GetIpAddress());
            return NoContent();
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

    [HttpPut("{id}/archivar")]
    public async Task<IActionResult> Archivar(int id)
    {
        try
        {
            await _documentoService.ArchivarAsync(id, GetCurrentUserId(), GetIpAddress());
            return NoContent();
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await _documentoService.EliminarAsync(id, GetCurrentUserId(), GetIpAddress());
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    // --- Versiones ---

    [HttpGet("{id}/versiones")]
    public async Task<ActionResult<IEnumerable<DocumentoVersionDto>>> GetVersiones(int id)
    {
        var versiones = await _documentoService.ObtenerVersionesAsync(id);
        return Ok(versiones);
    }

    [HttpPost("{id}/versiones")]
    public async Task<ActionResult<DocumentoVersionDto>> UploadVersion(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("El archivo es obligatorio.");
        }

        try
        {
            using var stream = file.OpenReadStream();
            var version = await _documentoService.CargarVersionAsync(id, file.FileName, stream, GetCurrentUserId(), GetIpAddress());
            return Ok(version);
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

    [HttpGet("{id}/versiones/{versionId}/descargar")]
    public async Task<IActionResult> DownloadVersion(int id, int versionId)
    {
        try
        {
            var (stream, fileName, contentType) = await _documentoService.DescargarVersionAsync(id, versionId, GetCurrentUserId(), GetIpAddress());
            // El FileResult se encargará de cerrar el stream una vez finalizada la descarga
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // --- Auditoría ---

    [HttpGet("{id}/auditoria")]
    public async Task<ActionResult<IEnumerable<AuditoriaDocumentalDto>>> GetAuditoria(int id)
    {
        var auditoria = await _documentoService.ObtenerAuditoriaAsync(id);
        return Ok(auditoria);
    }

    [HttpGet("auditoria/todas")]
    public async Task<ActionResult<IEnumerable<AuditoriaDocumentalDto>>> GetTodasAuditorias()
    {
        var auditoria = await _documentoService.ObtenerTodasAuditoriasAsync();
        return Ok(auditoria);
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
