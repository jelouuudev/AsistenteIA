using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Operador,Supervisor")]
public class PlannerController : Controller
{
    private readonly IApiService _apiService;

    public PlannerController(IApiService apiService) => _apiService = apiService;

    private (int userId, string ip) GetUserInfo()
        => (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");

    /// <summary>Dashboard de planes (Actividad 10) y formulario de generación.</summary>
    public async Task<IActionResult> Index()
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            ViewBag.Dashboard = await _apiService.GetPlannerDashboardAsync(userId, ip);
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            ViewBag.Dashboard = new PlannerDashboardDto();
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generar(string objetivo)
    {
        var (userId, ip) = GetUserInfo();
        if (string.IsNullOrWhiteSpace(objetivo))
        {
            TempData["ErrorMessage"] = "Escriba el objetivo del plan.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var plan = await _apiService.GenerarPlanAsync(objetivo, userId, ip);
            TempData["SuccessMessage"] = $"Plan #{plan.IdPlan} generado.";
            return RedirectToAction(nameof(Detalle), new { id = plan.IdPlan });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>Visualizador de planes (Actividad 5 / Sección 14): muestra el DAG antes de ejecutar.</summary>
    public async Task<IActionResult> Detalle(int id)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            ViewBag.Plan = await _apiService.GetPlanAsync(id, userId, ip);
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }
        return View();
    }

    /// <summary>Simulación previa a la ejecución (Actividad 5).</summary>
    public async Task<IActionResult> Simular(int id)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            ViewBag.Plan = await _apiService.SimularPlanAsync(id, userId, ip);
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }
        return View("Detalle");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ejecutar(int id)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            var plan = await _apiService.EjecutarPlanAsync(id, userId, ip);
            TempData["SuccessMessage"] = $"Plan #{plan.IdPlan} en ejecución.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Detalle), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var (userId, ip) = GetUserInfo();
        await _apiService.AprobarPlanAsync(id, userId, ip);
        TempData["SuccessMessage"] = "Plan aprobado.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var (userId, ip) = GetUserInfo();
        await _apiService.CancelarPlanAsync(id, userId, ip);
        TempData["SuccessMessage"] = "Plan cancelado.";
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
