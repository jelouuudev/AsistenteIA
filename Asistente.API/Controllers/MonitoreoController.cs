using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MonitoreoController : ControllerBase
{
    private readonly IIndexacionService _indexacionService;
    private readonly IRagService _ragService;
    private readonly IVectorStore _vectorStore;

    public MonitoreoController(
        IIndexacionService indexacionService,
        IRagService ragService,
        IVectorStore vectorStore)
    {
        _indexacionService = indexacionService;
        _ragService = ragService;
        _vectorStore = vectorStore;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardIndexacionDto>> GetDashboard()
    {
        var dashboard = await _indexacionService.ObtenerDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("diagnostico")]
    public async Task<ActionResult<object>> Diagnostico()
    {
        try
        {
            var dashboard = await _indexacionService.ObtenerDashboardAsync();
            var vectorStoreStatus = await _vectorStore.HealthCheckAsync();
            var documentCount = await _vectorStore.GetDocumentCountAsync();
            
            return Ok(new
            {
                VectorStoreActivo = vectorStoreStatus,
                DocumentosEnVectorStore = documentCount,
                Dashboard = dashboard,
                Timestamp = System.DateTime.UtcNow
            });
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("buscar")]
    public async Task<ActionResult<BusquedaSemanticaResponse>> Buscar(
        [FromBody] BusquedaSemanticaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Consulta))
            return BadRequest("La consulta no puede estar vacía.");

        var resultado = await _ragService.BuscarSemanticamenteAsync(request.Consulta, request.TopK);
        return Ok(resultado);
    }
}
