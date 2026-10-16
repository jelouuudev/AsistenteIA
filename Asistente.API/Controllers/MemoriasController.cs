using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services.Memoria;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

/// <summary>Administración de la Memoria Empresarial (ETAPA 21).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MemoriasController : ControllerBase
{
    private readonly MemoryManager _manager;
    private readonly MemoryRetentionEngine _retencion;

    public MemoriasController(MemoryManager manager, MemoryRetentionEngine retencion)
    {
        _manager = manager;
        _retencion = retencion;
    }

    private int UsuarioId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<ActionResult> Listar([FromQuery] string? ambito, CancellationToken ct)
        => Ok(await _manager.ListarAsync(UsuarioId(), ambito, ct));

    [HttpGet("{codigo}")]
    public async Task<ActionResult> Obtener(string codigo, CancellationToken ct)
    {
        try { return Ok(await _manager.ObtenerAsync(codigo, UsuarioId(), null, ct)); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe la memoria '{codigo}'."); }
        catch (System.UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPost]
    public async Task<ActionResult> Crear([FromBody] CrearMemoryRequest request, CancellationToken ct)
    {
        try
        {
            var m = await _manager.CrearAsync(request, UsuarioId(), ct);
            return CreatedAtAction(nameof(Obtener), new { codigo = m.Codigo }, m);
        }
        catch (System.Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{codigo}")]
    public async Task<ActionResult> Actualizar(string codigo, [FromBody] ActualizarMemoryRequest request, CancellationToken ct)
    {
        try { return Ok(await _manager.ActualizarAsync(codigo, request, UsuarioId(), ct)); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe la memoria '{codigo}'."); }
        catch (System.UnauthorizedAccessException) { return Forbid(); }
        catch (System.Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{codigo}/revertir/{numero:int}")]
    public async Task<ActionResult> Revertir(string codigo, int numero, [FromBody] RevertirMemoryRequest? request, CancellationToken ct)
    {
        try { return Ok(await _manager.RevertirAsync(codigo, numero, request?.Motivo, UsuarioId(), ct)); }
        catch (System.Collections.Generic.KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (System.UnauthorizedAccessException) { return Forbid(); }
        catch (System.Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{codigo}")]
    public async Task<ActionResult> Eliminar(string codigo, CancellationToken ct)
    {
        try { await _manager.EliminarAsync(codigo, UsuarioId(), ct); return NoContent(); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe la memoria '{codigo}'."); }
        catch (System.UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("{codigo}/versiones")]
    public async Task<ActionResult> Versiones(string codigo, CancellationToken ct)
    {
        try { return Ok(await _manager.VersionesAsync(codigo, UsuarioId(), ct)); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe la memoria '{codigo}'."); }
        catch (System.UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("{codigo}/auditoria")]
    public async Task<ActionResult> Auditoria(string codigo, CancellationToken ct)
    {
        try { return Ok(await _manager.AuditoriaAsync(codigo, UsuarioId(), 100, ct)); }
        catch (System.UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("buscar")]
    public async Task<ActionResult> Buscar([FromQuery] string texto, [FromQuery] int topK = 5,
        [FromQuery] string? ambito = null, CancellationToken ct = default)
        => Ok(await _manager.BuscarAsync(texto ?? string.Empty, UsuarioId(), null, topK, ambito, ct));

    [HttpGet("metricas")]
    public async Task<ActionResult> Metricas(CancellationToken ct)
        => Ok(await _manager.MetricasAsync(ct));

    [HttpGet("retencion/pendientes")]
    public async Task<ActionResult> RetencionPendientes(CancellationToken ct)
        => Ok((await _retencion.ExpiradasAsync(ct)).ConvertAll(m => m.Codigo));

    [HttpPost("retencion/aplicar")]
    public async Task<ActionResult> AplicarRetencion(CancellationToken ct)
    {
        var (desactivadas, eliminadas) = await _retencion.AplicarAsync(UsuarioId(), ct);
        return Ok(new { desactivadas, eliminadas });
    }
}

/// <summary>Cuerpo para POST api/memorias/{codigo}/revertir/{numero}.</summary>
public class RevertirMemoryRequest
{
    public string? Motivo { get; set; }
}
