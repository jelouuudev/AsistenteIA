using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class DisparadoresEventoController : Controller
{
    private readonly IApiService _apiService;
    public DisparadoresEventoController(IApiService apiService) => _apiService = apiService;

    private int GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        try { return View(await _apiService.GetDisparadoresEventoAsync(GetCurrentUserId(), GetIp())); }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new List<DisparadorEventoDto>()); }
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int idEvento = 0)
    {
        ViewBag.IdEvento = idEvento;
        ViewBag.Eventos = (await _apiService.GetEventosEmpresarialesAsync(GetCurrentUserId(), GetIp()))
            .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = e.IdEvento.ToString(), Text = $"{e.Nombre} ({e.Codigo})" }).ToList();
        return View(new CrearDisparadorEventoRequest { IdEvento = idEvento, Tipo = "Cron", ConfigJson = "{}", Activo = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearDisparadorEventoRequest model)
    {
        if (!ModelState.IsValid) { ViewBag.IdEvento = model.IdEvento; return View(model); }
        try
        {
            await _apiService.CrearDisparadorEventoAsync(model, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = "Disparador de evento creado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); ViewBag.IdEvento = model.IdEvento; return View(model); }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var disparador = await _apiService.GetDisparadorEventoByIdAsync(id, GetCurrentUserId(), GetIp());
        if (disparador == null) return NotFound();
        ViewBag.Eventos = (await _apiService.GetEventosEmpresarialesAsync(GetCurrentUserId(), GetIp()))
            .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = e.IdEvento.ToString(), Text = $"{e.Nombre} ({e.Codigo})" }).ToList();
        return View(disparador);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarDisparadorEventoRequest model)
    {
        if (!ModelState.IsValid) return View(new DisparadorEventoDto { IdDisparador = id });
        try
        {
            await _apiService.ActualizarDisparadorEventoAsync(id, model, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = "Disparador actualizado.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(new DisparadorEventoDto { IdDisparador = id }); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _apiService.ActivarDisparadorEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Disparador activado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _apiService.DesactivarDisparadorEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Desactivado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        try { await _apiService.EliminarDisparadorEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Eliminado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
