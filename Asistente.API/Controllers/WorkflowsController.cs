using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowsController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowDto>>> GetAll()
        => Ok(await _workflowService.ObtenerTodosAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowDto>> GetById(int id)
    {
        var w = await _workflowService.ObtenerPorIdAsync(id);
        return w == null ? NotFound("Flujo no encontrado.") : Ok(w);
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowDto>> Create([FromBody] CrearWorkflowRequest request)
    {
        try
        {
            var creado = await _workflowService.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = creado.IdWorkflow }, creado);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ActualizarWorkflowRequest request)
    {
        try { await _workflowService.ActualizarAsync(id, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _workflowService.CambiarEstadoAsync(id, Asistente.Domain.Entities.EstadoWorkflow.Activo); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _workflowService.CambiarEstadoAsync(id, Asistente.Domain.Entities.EstadoWorkflow.Suspendido); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}/versionar")]
    public async Task<IActionResult> Versionar(int id)
    {
        try { await _workflowService.VersionarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _workflowService.EliminarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowEjecucionesController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowEjecucionesController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowEjecucionDto>>> GetAll()
        => Ok(await _workflowService.ObtenerEjecucionesAsync());
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracionWorkflowController : ControllerBase
{
    private readonly IConfiguracionWorkflowService _configService;

    public ConfiguracionWorkflowController(IConfiguracionWorkflowService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracionWorkflowDto>> Get()
        => Ok(await _configService.ObtenerAsync());

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] ConfiguracionWorkflowDto config)
    {
        try { await _configService.GuardarAsync(config); return NoContent(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }
}
