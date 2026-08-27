using System.Security.Claims;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Operador,Supervisor")]
public class AprobacionesController : Controller
{
    private readonly IApiService _apiService;
    public AprobacionesController(IApiService apiService) => _apiService = apiService;

    private (int userId, string ip) GetUserInfo()
        => (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");

    /// <summary>Centro de Aprobaciones (Actividad 11): dashboard con conteos por estado.</summary>
    public async Task<IActionResult> Index()
    {
        var (userId, ip) = GetUserInfo();
        try { ViewBag.Dashboard = await _apiService.GetAprobacionesDashboardAsync(userId, ip); }
        catch (Exception ex) { ViewBag.Error = ex.Message; }
        return View();
    }

    /// <summary>Bandeja personal (Actividad 12): mis aprobaciones pendientes.</summary>
    public async Task<IActionResult> Bandeja()
    {
        var (userId, ip) = GetUserInfo();
        try { ViewBag.Bandeja = await _apiService.GetBandejaAprobacionesAsync(userId, ip); }
        catch (Exception ex) { ViewBag.Error = ex.Message; }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decidir(int id, string decision, string? comentario)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            await _apiService.DecidirAprobacionAsync(id, decision, comentario, userId, ip);
            TempData["SuccessMessage"] = $"Solicitud #{id} {decision.ToLower()}.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Bandeja));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delegar(int id, int idUsuarioDestino, string? comentario)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            await _apiService.DelegarAprobacionAsync(id, idUsuarioDestino, comentario, userId, ip);
            TempData["SuccessMessage"] = $"Solicitud #{id} delegada.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Bandeja));
    }
}
