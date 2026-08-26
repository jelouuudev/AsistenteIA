using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class TareasProgramadasController : Controller
{
    private readonly IApiService _apiService;
    public TareasProgramadasController(IApiService apiService) => _apiService = apiService;

    private int GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        try { return View(await _apiService.GetTareasProgramadasAsync(GetCurrentUserId(), GetIp())); }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new List<TareaProgramadaDto>()); }
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        ViewBag.Workflows = await ObtenerWorkflowsSelect();
        return View(new CrearTareaProgramadaRequest
        {
            Nombre = "",
            ExpresionCron = "0 0 2 * * ?",
            IdWorkflow = 0,
            Activa = true,
            UsuarioCreacion = GetCurrentUserId()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearTareaProgramadaRequest model)
    {
        if (model.IdWorkflow <= 0)
            ModelState.AddModelError(string.Empty, "Debe seleccionar un flujo de trabajo.");
        if (!ModelState.IsValid) { ViewBag.Workflows = await ObtenerWorkflowsSelect(); return View(model); }
        try
        {
            model.UsuarioCreacion = GetCurrentUserId();
            await _apiService.CrearTareaProgramadaAsync(model, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = "Tarea programada registrada. Se reprogramará al reiniciar la API.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); ViewBag.Workflows = await ObtenerWorkflowsSelect(); return View(model); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _apiService.ActivarTareaProgramadaAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Tarea activada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _apiService.DesactivarTareaProgramadaAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Tarea desactivada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        try { await _apiService.EliminarTareaProgramadaAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Tarea eliminada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> ObtenerWorkflowsSelect()
    {
        var workflows = await _apiService.GetWorkflowsAsync(GetCurrentUserId(), GetIp());
        return workflows.Select(w => new SelectListItem { Value = w.IdWorkflow.ToString(), Text = w.Nombre }).ToList();
    }
}
