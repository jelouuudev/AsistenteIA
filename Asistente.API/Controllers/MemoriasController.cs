using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Memoria;
using Asistente.Domain.Interfaces;
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
    private readonly IDashboardMemoriaService _dashboard;
    private readonly IPermisoService _permisos;

    public MemoriasController(MemoryManager manager, MemoryRetentionEngine retencion,
        IDashboardMemoriaService dashboard, IPermisoService permisos)
    {
        _manager = manager;
        _retencion = retencion;
        _dashboard = dashboard;
        _permisos = permisos;
    }

    private int UsuarioId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>
    /// Panel de memoria: métricas, auditoría reciente y retención pendiente.
    ///
    /// Requiere AUDITORIA_CONSULTAR, igual que el panel de Seguridad, y no un
    /// MEMORIA:{codigo}: el panel enseña TODAS las memorias, así que exigir el
    /// permiso de cada una dejaría la tabla en blanco salvo para el dueño de todo.
    /// Operador no la ve (403); Supervisor y Administrador sí.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardMemoriaDto>> Dashboard(CancellationToken ct)
    {
        if (!await _permisos.TienePermisoAsync(UsuarioId(), "AUDITORIA_CONSULTAR", ct))
            return StatusCode(403, "No tiene el permiso requerido: AUDITORIA_CONSULTAR.");
        return Ok(await _dashboard.ObtenerAsync(ct));
    }

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
        // Sin esta captura, un deniego de autorización (escritura en ámbito
        // compartido sin MEMORIA_ADMINISTRAR) salía como 400 Bad Request, que
        // blames al cliente de una falta de permisos. 403 es el código correcto.
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
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

    [HttpPost("{codigo}/indexar")]
    public async Task<ActionResult> Indexar(string codigo, CancellationToken ct)
    {
        try { return Ok(await _manager.IndexarAsync(codigo, UsuarioId(), null, ct)); }
        catch (System.Collections.Generic.KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
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
