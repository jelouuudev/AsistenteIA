using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Supervisor")]
public class AuditoriaController : Controller
{
    private readonly IApiService _apiService;

    public System.Security.Claims.ClaimsPrincipal CurrentUser => User;

    public AuditoriaController(IApiService apiService)
    {
        _apiService = apiService;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out var id) ? id : 1;
    }

    private string GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var sesionesTask = _apiService.GetAuditoriaSesionesAsync(GetCurrentUserId(), GetIpAddress());
            var actividadesTask = _apiService.GetAuditoriaActividadesAsync(GetCurrentUserId(), GetIpAddress());

            await Task.WhenAll(sesionesTask, actividadesTask);

            ViewBag.Sesiones = await sesionesTask;
            ViewBag.Actividades = await actividadesTask;

            return View();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar auditorías: {ex.Message}";
            ViewBag.Sesiones = new List<AuditoriaSesionDto>();
            ViewBag.Actividades = new List<AuditoriaActividadDto>();
            return View();
        }
    }

    [HttpGet]
    public IActionResult Test()
    {
        return Content("Test endpoint works. User: " + User.Identity.Name);
    }
}
