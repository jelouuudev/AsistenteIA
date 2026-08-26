using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Supervisor")]
public class MonitoreoWorkflowsController : Controller
{
    private readonly IApiService _apiService;

    public MonitoreoWorkflowsController(IApiService apiService)
    {
        _apiService = apiService;
    }

    private int GetCurrentUserId()
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;

    private string GetIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        try
        {
            var ejecuciones = await _apiService.GetWorkflowEjecucionesAsync(GetCurrentUserId(), GetIpAddress());
            var workflows = await _apiService.GetWorkflowsAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Workflows = workflows;
            return View(ejecuciones);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar monitoreo de flujos: {ex.Message}";
            return View(new List<WorkflowEjecucionDto>());
        }
    }
}
