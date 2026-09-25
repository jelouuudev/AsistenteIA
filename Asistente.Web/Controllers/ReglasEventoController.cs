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

    public async Task<IActionResult> Index(int? idEvento = null)
    {
        try
        {
            var reglas = await _apiService.GetReglasEventoAsync(GetCurrentUserId(), GetIp());
            if (idEvento.HasValue && idEvento.Value != 0)
            {
                reglas = reglas.Where(r => r.IdEvento == idEvento.Value).ToList();
                var evento = await _apiService.GetEventoEmpresarialByIdAsync(idEvento.Value, GetCurrentUserId(), GetIp());
                ViewBag.IdEventoFiltro = idEvento.Value;
                ViewBag.NombreEventoFiltro = evento == null ? $"Evento {idEvento.Value}" : $"{evento.Nombre} ({evento.Codigo})";
            }
            return View(reglas);
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new List<ReglaEventoDto>()); }
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int idEvento = 0)
    {
        ViewBag.IdEvento = idEvento;
        var eventos = await _apiService.GetEventosEmpresarialesAsync(GetCurrentUserId(), GetIp());
        ViewBag.Eventos = eventos
            .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = e.IdEvento.ToString(), Text = $"{e.Nombre} ({e.Codigo})" }).ToList();
        // Nombre para mostrar el evento bloqueado cuando se viene desde un evento concreto.
        ViewBag.NombreEventoFijo = eventos.FirstOrDefault(e => e.IdEvento == idEvento) is { } ev
            ? $"{ev.Nombre} ({ev.Codigo})"
            : null;
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
    public async Task<IActionResult> Crear(CrearReglaEventoRequest model, int? idEventoFijo)
    {
        // Si venía de un evento concreto (campo oculto idEventoFijo), el evento queda
        // bloqueado del lado servidor: se fuerza su valor aunque el post haya sido manipulado.
        if (idEventoFijo.HasValue && idEventoFijo.Value != 0) model.IdEvento = idEventoFijo.Value;
        if (!ModelState.IsValid) { await CargarListasAsync(model.IdEvento); return View(model); }
        try
        {
            await _apiService.CrearReglaEventoAsync(model, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = "Regla de automatización creada.";
            return idEventoFijo.HasValue && idEventoFijo.Value != 0
                ? RedirectToAction(nameof(Index), new { idEvento = idEventoFijo.Value })
                : RedirectToAction(nameof(Index));
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); await CargarListasAsync(model.IdEvento); return View(model); }
    }

    private async Task CargarListasAsync(int idEventoSeleccionado)
    {
        ViewBag.IdEvento = idEventoSeleccionado;
        var eventos = new List<EventoEmpresarialDto>();
        try { eventos = await _apiService.GetEventosEmpresarialesAsync(GetCurrentUserId(), GetIp()); } catch { /* la vista muestra vacío */ }
        ViewBag.Eventos = eventos
            .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = e.IdEvento.ToString(), Text = $"{e.Nombre} ({e.Codigo})" }).ToList();
        ViewBag.NombreEventoFijo = eventos.FirstOrDefault(e => e.IdEvento == idEventoSeleccionado) is { } ev
            ? $"{ev.Nombre} ({ev.Codigo})"
            : null;
        try
        {
            ViewBag.Workflows = (await _apiService.GetWorkflowsAsync(GetCurrentUserId(), GetIp()))
                .Select(w => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = w.IdWorkflow.ToString(), Text = w.Nombre }).ToList();
        }
        catch { ViewBag.Workflows = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>(); }
    }

    private IActionResult VolverAlListado(int? idEvento)
        => idEvento.HasValue && idEvento.Value != 0
            ? RedirectToAction(nameof(Index), new { idEvento = idEvento.Value })
            : RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id, int? idEvento = null)
    {
        try { await _apiService.ActivarReglaEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Regla activada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return VolverAlListado(idEvento);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id, int? idEvento = null)
    {
        try { await _apiService.DesactivarReglaEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Regla desactivada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return VolverAlListado(idEvento);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id, int? idEvento = null)
    {
        try { await _apiService.EliminarReglaEventoAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Regla eliminada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return VolverAlListado(idEvento);
    }
}
