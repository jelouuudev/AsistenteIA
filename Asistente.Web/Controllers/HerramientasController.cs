using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class HerramientasController : Controller
{
    private readonly IApiService _apiService;

    public HerramientasController(IApiService apiService)
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
            var herramientas = await _apiService.GetHerramientasAsync(null, GetCurrentUserId(), GetIpAddress());
            return View(herramientas);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar herramientas: {ex.Message}";
            return View(new List<HerramientaDto>());
        }
    }

    // Registro deshabilitado en UI: crear una herramienta es técnico
    // (clase ITool + DI + BD, ETAPA 11). El formulario solo creaba fichas sin
    // implementación que fallaban al ejecutarse.
    [HttpGet]
    public IActionResult Crear()
    {
        TempData["ErrorMessage"] = "El registro de herramientas es técnico (código + DI + BD). Solicítalo al equipo de desarrollo.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Crear(CrearHerramientaRequest model)
    {
        TempData["ErrorMessage"] = "El registro de herramientas es técnico (código + DI + BD). Solicítalo al equipo de desarrollo.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _apiService.ActivarHerramientaAsync(id, GetCurrentUserId(), GetIpAddress()); TempData["SuccessMessage"] = "Herramienta activada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _apiService.DesactivarHerramientaAsync(id, GetCurrentUserId(), GetIpAddress()); TempData["SuccessMessage"] = "Herramienta desactivada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
