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
public class FuentesConocimientoController : ControllerBase
{
    private readonly IFuenteConocimientoService _fuenteService;

    public FuentesConocimientoController(IFuenteConocimientoService fuenteService)
    {
        _fuenteService = fuenteService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FuenteConocimientoDto>>> GetAll()
    {
        var fuentes = await _fuenteService.ObtenerTodasAsync();
        return Ok(fuentes);
    }

    [HttpGet("activas")]
    public async Task<ActionResult<IEnumerable<FuenteConocimientoDto>>> GetActivas()
    {
        var fuentes = await _fuenteService.ObtenerActivasAsync();
        return Ok(fuentes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FuenteConocimientoDto>> GetById(int id)
    {
        var fuente = await _fuenteService.ObtenerPorIdAsync(id);
        if (fuente == null) return NotFound("Fuente de conocimiento no encontrada.");
        return Ok(fuente);
    }

    [HttpPost]
    public async Task<ActionResult<FuenteConocimientoDto>> Create([FromBody] CrearFuenteConocimientoRequest request)
    {
        try
        {
            var userId = 1;
            if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id))
                userId = id;

            var fuente = await _fuenteService.CrearAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = fuente.IdFuente }, fuente);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FuenteConocimientoDto>> Update(int id, [FromBody] ActualizarFuenteConocimientoRequest request)
    {
        try
        {
            var fuente = await _fuenteService.ActualizarAsync(id, request);
            return Ok(fuente);
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
            await _fuenteService.ActivarAsync(id);
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
            await _fuenteService.DesactivarAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardFuentesDto>> GetDashboard()
    {
        var dashboard = await _fuenteService.ObtenerDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("asistente/{idAsistente}/fuentes")]
    public async Task<ActionResult<IEnumerable<AsistenteFuenteDto>>> GetFuentesDeAsistente(int idAsistente)
    {
        var fuentes = await _fuenteService.ObtenerFuentesDeAsistenteAsync(idAsistente);
        return Ok(fuentes);
    }

    [HttpGet("fuente/{idFuente}/asistentes")]
    public async Task<ActionResult<IEnumerable<AsistenteFuenteDto>>> GetAsistentesDeFuente(int idFuente)
    {
        var asistentes = await _fuenteService.ObtenerAsistentesDeFuenteAsync(idFuente);
        return Ok(asistentes);
    }

    [HttpPost("asistente/asignar")]
    public async Task<IActionResult> AsignarFuenteAAsistente([FromBody] AsignarFuenteAAsistenteRequest request)
    {
        try
        {
            await _fuenteService.AsignarFuenteAAsistenteAsync(request);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("asistente/{idAsistente}/fuente/{idFuente}")]
    public async Task<IActionResult> DesasignarFuenteDeAsistente(int idAsistente, int idFuente)
    {
        try
        {
            await _fuenteService.DesasignarFuenteDeAsistenteAsync(idAsistente, idFuente);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("asistente/{idAsistente}/fuente/{idFuente}/activar")]
    public async Task<IActionResult> ActivarAsistenteFuente(int idAsistente, int idFuente)
    {
        try
        {
            await _fuenteService.ActivarAsistenteFuenteAsync(idAsistente, idFuente);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("asistente/{idAsistente}/fuente/{idFuente}/desactivar")]
    public async Task<IActionResult> DesactivarAsistenteFuente(int idAsistente, int idFuente)
    {
        try
        {
            await _fuenteService.DesactivarAsistenteFuenteAsync(idAsistente, idFuente);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("fuente/{idFuente}/documentos")]
    public async Task<ActionResult<IEnumerable<DocumentoFuenteDto>>> GetDocumentosDeFuente(int idFuente)
    {
        var documentos = await _fuenteService.ObtenerDocumentosDeFuenteAsync(idFuente);
        return Ok(documentos);
    }

    [HttpGet("documento/{idDocumento}/fuentes")]
    public async Task<ActionResult<IEnumerable<FuenteConocimientoDto>>> GetFuentesDeDocumento(int idDocumento)
    {
        var fuentes = await _fuenteService.ObtenerFuentesDeDocumentoAsync(idDocumento);
        return Ok(fuentes);
    }

    [HttpPost("documento/asignar")]
    public async Task<IActionResult> AsignarDocumentoAFuente([FromBody] AsignarDocumentoAFuenteRequest request)
    {
        try
        {
            await _fuenteService.AsignarDocumentoAFuenteAsync(request);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("documento/{idDocumento}/fuente/{idFuente}")]
    public async Task<IActionResult> DesasignarDocumentoDeFuente(int idDocumento, int idFuente)
    {
        try
        {
            await _fuenteService.DesasignarDocumentoDeFuenteAsync(idDocumento, idFuente);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
