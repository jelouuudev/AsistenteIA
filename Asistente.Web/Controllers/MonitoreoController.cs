using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Supervisor")]
public class MonitoreoController : Controller
{
    private readonly IApiService _apiService;

    public MonitoreoController(IApiService apiService)
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
            var userId = GetCurrentUserId();
            var ip = GetIpAddress();

            var dashboard = await _apiService.GetIndexacionDashboardAsync(userId, ip);

            return View(dashboard);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar monitoreo: {ex.Message}";
            return View(new DashboardIndexacionDto());
        }
    }

    [HttpPost]
    public async Task<ActionResult<BusquedaSemanticaResponse>> Buscar([FromBody] BusquedaSemanticaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Consulta))
        {
            return BadRequest(new BusquedaSemanticaResponse
            {
                Consulta = request.Consulta,
                TotalResultados = 0
            });
        }

        var (userId, ip) = GetUserInfo();
        try
        {
            var resultado = await _apiService.BuscarSemanticamenteAsync(request, userId, ip);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private (int userId, string ip) GetUserInfo()
    {
        var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 1;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return (userId, ip);
    }
}
