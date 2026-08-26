using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class AsistenteHerramientasController : Controller
{
    private readonly IApiService _apiService;

    public AsistenteHerramientasController(IApiService apiService)
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
            var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
            return View(asistentes);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar asistentes: {ex.Message}";
            return View(new List<AsistenteDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Gestionar(int idAsistente)
    {
        try
        {
            var asistente = await _apiService.GetAsistenteByIdAsync(idAsistente, GetCurrentUserId(), GetIpAddress());
            if (asistente == null) return NotFound();

            var todas = await _apiService.GetHerramientasAsync(idAsistente, GetCurrentUserId(), GetIpAddress());
            var asociadas = await _apiService.GetHerramientasDeAsistenteAsync(idAsistente, GetCurrentUserId(), GetIpAddress());
            var asociadasIds = asociadas.Select(a => a.IdHerramienta).ToHashSet();

            ViewBag.Asistente = asistente;
            ViewBag.AsociadasIds = asociadasIds;
            return View(todas);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Asociar(int idAsistente, int idHerramienta, bool activa = true)
    {
        try
        {
            await _apiService.AsociarHerramientaAsync(idAsistente, idHerramienta, activa, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Herramienta asociada al asistente.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Gestionar), new { idAsistente });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desasociar(int idAsistente, int idHerramienta)
    {
        try
        {
            await _apiService.DesasociarHerramientaAsync(idAsistente, idHerramienta, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Herramienta desasociada del asistente.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Gestionar), new { idAsistente });
    }
}
