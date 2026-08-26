using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Supervisor")]
public class MonitoreoEventosController : Controller
{
    private readonly IApiService _apiService;
    public MonitoreoEventosController(IApiService apiService) => _apiService = apiService;

    private int GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        try { return View(await _apiService.GetMonitoreoEventosAsync(GetCurrentUserId(), GetIp())); }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new MonitoreoEventosDto()); }
    }

    public async Task<IActionResult> EventosProcesados()
    {
        try { return View(await _apiService.GetEventosProcesadosAsync(GetCurrentUserId(), GetIp())); }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new List<EventoProcesadoDto>()); }
    }

    public async Task<IActionResult> Configuracion()
    {
        try { return View(await _apiService.GetConfiguracionEventoMotorAsync(GetCurrentUserId(), GetIp())); }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new ConfiguracionEventoMotorDto()); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configuracion(ConfiguracionEventoMotorDto model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await _apiService.GuardarConfiguracionEventoMotorAsync(model, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = "Configuración del motor de eventos guardada.";
            return RedirectToAction(nameof(Configuracion));
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(model); }
    }
}
