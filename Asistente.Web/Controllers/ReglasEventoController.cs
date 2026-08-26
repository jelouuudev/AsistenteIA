using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ReglasEventoController : Controller
{
    private readonly IApiService _apiService;
    public ReglasEventoController(IApiService apiService) => _apiService = apiService;

    private int GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        try { return View(await _apiService.GetReglasEventoAsync(GetCurrentUserId(), GetIp())); }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new List<ReglaEventoDto>()); }
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int idEvento = 0)
    {
        ViewBag.IdEvento = idEvento;
        ViewBag.Eventos = (await _apiService.GetEventosEmpresarialesAsync(GetCurrentUserId(), GetIp()))
            .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = e.IdEvento.ToString(), Text = $"{e.Nombre} ({e.Codigo})" }).ToList();
        ViewBag.Workflows = (await _apiService.GetWorkflowsAsync(GetCurrentUserId(), GetIp()))
            .Select(w => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = w.IdWorkflow.ToString(), Text = w.Nombre }).ToList();
        return View(new CrearReglaEventoRequest
        {
            IdEvento = idEvento,
            IdWorkflow = 0,
            Condicion = "",
            Prioridad = 1,
            Activa = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearReglaEventoRequest model)
    {
        if (!ModelState.IsValid) { ViewBag.IdEvento = model.IdEvento; return View(model); }
        try
        {
            await _apiService.CrearReglaEventoAsync(model, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = "Regla de automatización creada.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); ViewBag.IdEvento = model.IdEvento; return View(model); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _apiService.ActivarReglaEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Regla activada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _apiService.DesactivarReglaEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Regla desactivada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        try { await _apiService.EliminarReglaEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Regla eliminada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
