using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ConfiguracionMemoriaController : Controller
{
    private readonly IApiService _apiService;

    public ConfiguracionMemoriaController(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<IActionResult> Index()
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            var config = await _apiService.GetConfiguracionMemoriaAsync(userId, ip);
            return View(config);
        }
        catch
        {
            return View(new ConfiguracionMemoriaDto
            {
                MaximoMensajesContexto = 20,
                MaximoTokensContexto = 4096,
                LongitudResumen = 500,
                CantidadConversacionesVisibles = 50,
                Activo = true
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Guardar(ActualizarConfiguracionMemoriaRequest request)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            await _apiService.ActualizarConfiguracionMemoriaAsync(request, userId, ip);
            TempData["SuccessMessage"] = "Configuración de memoria actualizada exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al guardar configuración: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    private (int userId, string ip) GetUserInfo()
    {
        var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 1;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return (userId, ip);
    }
}
