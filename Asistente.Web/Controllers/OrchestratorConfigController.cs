using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class OrchestratorConfigController : Controller
{
    private readonly IApiService _apiService;

    public OrchestratorConfigController(IApiService apiService)
    {
        _apiService = apiService;
    }

    private int GetCurrentUserId()
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;

    private string GetIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var config = await _apiService.GetConfiguracionOrchestratorAsync(GetCurrentUserId(), GetIpAddress());
            return View(config);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return View(new ConfiguracionOrchestratorDto());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(ConfiguracionOrchestratorDto model)
    {
        try
        {
            await _apiService.GuardarConfiguracionOrchestratorAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Configuración del orquestador guardada.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
