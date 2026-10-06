using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Conectores;
using Asistente.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

/// <summary>Administración del API Gateway Empresarial y sus conectores (ETAPA 20).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConectoresController : ControllerBase
{
    private readonly ConnectorService _service;
    private readonly IConnectorGateway _gateway;

    public ConectoresController(ConnectorService service, IConnectorGateway gateway)
    {
        _service = service;
        _gateway = gateway;
    }

    private int UsuarioId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<ActionResult> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(ct));

    [HttpGet("{codigo}")]
    public async Task<ActionResult> Obtener(string codigo, CancellationToken ct)
    {
        var c = await _service.ObtenerPorCodigoAsync(codigo, ct);
        return c == null ? NotFound($"No existe el conector '{codigo}'.") : Ok(c);
    }

    [HttpPost]
    public async Task<ActionResult> Registrar([FromBody] CrearConectorRequest request, CancellationToken ct)
    {
        try
        {
            var c = await _service.RegistrarAsync(request, ct);
            return CreatedAtAction(nameof(Obtener), new { codigo = c.Codigo }, c);
        }
        catch (System.Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{codigo}/credencial")]
    public async Task<ActionResult> CambiarCredencial(string codigo, [FromBody] CrearCredencialRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.ActualizarCredencialAsync(codigo, request, ct)); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe el conector '{codigo}'."); }
        catch (System.Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{codigo}/habilitar")]
    public async Task<ActionResult> Habilitar(string codigo, CancellationToken ct)
    {
        try { await _service.CambiarEstadoAsync(codigo, true, ct); return Ok(new { codigo, activo = true }); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe el conector '{codigo}'."); }
    }

    [HttpPost("{codigo}/deshabilitar")]
    public async Task<ActionResult> Deshabilitar(string codigo, CancellationToken ct)
    {
        try { await _service.CambiarEstadoAsync(codigo, false, ct); return Ok(new { codigo, activo = false }); }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe el conector '{codigo}'."); }
    }

    [HttpPost("{codigo}/probar")]
    public async Task<ActionResult> Probar(string codigo, CancellationToken ct)
    {
        try
        {
            var (ok, error) = await _service.ProbarConexionAsync(codigo, ct);
            return Ok(new { ok, error });
        }
        catch (System.Collections.Generic.KeyNotFoundException) { return NotFound($"No existe el conector '{codigo}'."); }
        catch (System.Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{codigo}/ejecutar")]
    public async Task<ActionResult> Ejecutar(string codigo, [FromBody] EjecutarConectorRequest request, CancellationToken ct)
    {
        var resultado = await _gateway.EjecutarAsync(new ConnectorRequest
        {
            CodigoConector = codigo,
            Operacion = request.Operacion ?? string.Empty,
            Recurso = request.Recurso,
            Metodo = request.Metodo ?? "GET",
            Cuerpo = request.Cuerpo,
            TipoContenido = request.TipoContenido,
            Cabeceras = request.Cabeceras ?? new System.Collections.Generic.Dictionary<string, string>(),
            Parametros = request.Parametros ?? new System.Collections.Generic.Dictionary<string, string>(),
            IdUsuario = UsuarioId()
        }, ct);
        return Ok(new
        {
            exitoso = resultado.Exitoso,
            codigoRespuesta = resultado.CodigoRespuesta,
            contenido = resultado.Contenido,
            error = resultado.Error,
            latenciaMs = resultado.LatenciaMs,
            reintentos = resultado.Reintentos
        });
    }

    [HttpGet("{id:int}/metricas")]
    public async Task<ActionResult> Metricas(int id, CancellationToken ct)
        => Ok(await _service.MetricasAsync(id, ct));

    [HttpGet("{id:int}/auditoria")]
    public async Task<ActionResult> Auditoria(int id, [FromQuery] int tope = 100, CancellationToken ct = default)
        => Ok(await _service.AuditoriaAsync(id, tope, ct));
}

/// <summary>Cuerpo para POST api/conectores/{codigo}/ejecutar.</summary>
public class EjecutarConectorRequest
{
    public string? Operacion { get; set; }
    public string? Recurso { get; set; }
    public string? Metodo { get; set; }
    public string? Cuerpo { get; set; }
    public string? TipoContenido { get; set; }
    public System.Collections.Generic.Dictionary<string, string>? Cabeceras { get; set; }
    public System.Collections.Generic.Dictionary<string, string>? Parametros { get; set; }
}
