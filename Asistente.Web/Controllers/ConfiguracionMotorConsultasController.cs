using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ConfiguracionMotorConsultasController : Controller
{
    private readonly IApiService _apiService;

    public ConfiguracionMotorConsultasController(IApiService apiService)
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
            var config = await _apiService.GetConfiguracionMotorConsultasAsync(GetCurrentUserId(), GetIpAddress());
            if (config == null)
                config = new ConfiguracionMotorConsultasDto { Activo = true, TiempoMaximoEjecucionSegundos = 15, MaximoRegistros = 100, MaxConsultasSimultaneas = 5 };

            var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Conexiones = conexiones;
            return View(config);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar configuración: {ex.Message}";
            return View(new ConfiguracionMotorConsultasDto());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(ActualizarConfiguracionMotorConsultasRequest model)
    {
        try
        {
            await _apiService.ActualizarConfiguracionMotorConsultasAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Configuración del motor de consultas actualizada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
