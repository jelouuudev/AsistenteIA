using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventosEmpresarialesController : ControllerBase
{
    private readonly IEventoEmpresarialService _service;

    public EventosEmpresarialesController(IEventoEmpresarialService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventoEmpresarialDto>>> GetAll()
        => Ok(await _service.ObtenerTodosAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<EventoEmpresarialDto>> GetById(int id)
    {
        var e = await _service.ObtenerPorIdAsync(id);
        return e == null ? NotFound("Evento no encontrado.") : Ok(e);
    }

    [HttpGet("{id}/reglas")]
    public async Task<ActionResult<IEnumerable<ReglaEventoDto>>> GetReglas(int id)
        => Ok(await _service.ObtenerReglasAsync(id));

    [HttpPost]
    public async Task<ActionResult<EventoEmpresarialDto>> Create([FromBody] CrearEventoEmpresarialRequest request)
    {
        try
        {
            var creado = await _service.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = creado.IdEvento }, creado);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarEventoEmpresarialRequest request)
    {
        try { await _service.ActualizarAsync(id, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _service.CambiarEstadoAsync(id, true); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _service.CambiarEstadoAsync(id, false); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _service.EliminarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReglasEventoController : ControllerBase
{
    private readonly IReglaEventoService _service;

    public ReglasEventoController(IReglaEventoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReglaEventoDto>>> GetAll()
        => Ok(await _service.ObtenerTodosAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<ReglaEventoDto>> GetById(int id)
    {
        var r = await _service.ObtenerPorIdAsync(id);
        return r == null ? NotFound("Regla no encontrada.") : Ok(r);
    }

    [HttpPost]
    public async Task<ActionResult<ReglaEventoDto>> Create([FromBody] CrearReglaEventoRequest request)
    {
        try
        {
            var creada = await _service.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = creada.IdRegla }, creada);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarReglaEventoRequest request)
    {
        try { await _service.ActualizarAsync(id, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _service.CambiarEstadoAsync(id, true); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _service.CambiarEstadoAsync(id, false); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _service.EliminarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TareasProgramadasController : ControllerBase
{
    private readonly ITareaProgramadaService _service;

    public TareasProgramadasController(ITareaProgramadaService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TareaProgramadaDto>>> GetAll()
        => Ok(await _service.ObtenerTodosAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<TareaProgramadaDto>> GetById(int id)
    {
        var t = await _service.ObtenerPorIdAsync(id);
        return t == null ? NotFound("Tarea no encontrada.") : Ok(t);
    }

    [HttpPost]
    public async Task<ActionResult<TareaProgramadaDto>> Create([FromBody] CrearTareaProgramadaRequest request)
    {
        try
        {
            var creada = await _service.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = creada.IdTarea }, creada);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarTareaProgramadaRequest request)
    {
        try { await _service.ActualizarAsync(id, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _service.CambiarEstadoAsync(id, true); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _service.CambiarEstadoAsync(id, false); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _service.EliminarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventosProcesadosController : ControllerBase
{
    private readonly IEventoProcesadoService _service;

    public EventosProcesadosController(IEventoProcesadoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventoProcesadoDto>>> GetAll()
        => Ok(await _service.ObtenerTodosAsync());

    [HttpGet("evento/{idEvento}")]
    public async Task<ActionResult<IEnumerable<EventoProcesadoDto>>> GetByEvento(int idEvento)
        => Ok(await _service.ObtenerPorEventoAsync(idEvento));
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracionEventoMotorController : ControllerBase
{
    private readonly IConfiguracionEventoMotorService _service;

    public ConfiguracionEventoMotorController(IConfiguracionEventoMotorService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ConfiguracionEventoMotorDto>> Get()
        => Ok(await _service.ObtenerAsync());

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] ConfiguracionEventoMotorDto config)
    {
        try { await _service.GuardarAsync(config); return NoContent(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MonitoreoEventosController : ControllerBase
{
    private readonly IMonitoreoEventosService _service;

    public MonitoreoEventosController(IMonitoreoEventosService service) => _service = service;

    [HttpGet("panel")]
    public async Task<ActionResult<MonitoreoEventosDto>> Panel()
        => Ok(await _service.ObtenerPanelAsync());
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventoMotorController : ControllerBase
{
    private readonly IEventoMotorService _service;

    public EventoMotorController(IEventoMotorService service) => _service = service;

    /// <summary>
    /// Dispara un evento empresarial por su código (Actividad 4). El procesamiento es automático.
    /// </summary>
    [HttpPost("disparar")]
    public async Task<ActionResult<EventoProcesadoDto>> Disparar([FromBody] DispararEventoRequest request)
    {
        try
        {
            var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int? idUsuario = int.TryParse(usuario, out var id) ? id : null;
            var resultado = await _service.DispararEventoAsync(request.CodigoEvento, request.ContextoJson, idUsuario);
            return Ok(resultado);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}
