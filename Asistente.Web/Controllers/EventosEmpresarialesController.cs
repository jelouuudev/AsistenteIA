using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class EventosEmpresarialesController : Controller
{
    private readonly IApiService _apiService;
    public EventosEmpresarialesController(IApiService apiService) => _apiService = apiService;

    private int GetCurrentUserId() => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        try { return View(await _apiService.GetEventosEmpresarialesAsync(GetCurrentUserId(), GetIp())); }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; return View(new List<EventoEmpresarialDto>()); }
    }

    [HttpGet]
    public IActionResult Crear()
        => View(new CrearEventoEmpresarialRequest
        {
            Codigo = "",
            Nombre = "",
            Descripcion = "",
            Categoria = "Sistema",
            Activo = true,
            UsuarioCreacion = GetCurrentUserId()
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearEventoEmpresarialRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            model.UsuarioCreacion = GetCurrentUserId();
            await _apiService.CrearEventoEmpresarialAsync(model, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = "Evento empresarial registrado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(model); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _apiService.ActivarEventoEmpresarialAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Evento activado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _apiService.DesactivarEventoEmpresarialAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Evento desactivado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        try { await _apiService.EliminarEventoEmpresarialAsync(id, GetCurrentUserId(), GetIp()); TempData["SuccessMessage"] = "Evento eliminado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disparar(string codigo)
    {
        try
        {
            await _apiService.DispararEventoAsync(new DispararEventoRequest { CodigoEvento = codigo }, GetCurrentUserId(), GetIp());
            TempData["SuccessMessage"] = $"Evento '{codigo}' disparado. Revise el monitoreo de eventos.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
