using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ConfiguracionRAGController : Controller
{
    private readonly IApiService _apiService;

    public ConfiguracionRAGController(IApiService apiService)
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
            var config = await _apiService.GetConfiguracionRAGAsync(GetCurrentUserId(), GetIpAddress());
            return View(config);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar configuracion: {ex.Message}";
            return View(new ConfiguracionRAGDto());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(ActualizarConfiguracionRAGRequest model)
    {
        try
        {
            await _apiService.ActualizarConfiguracionRAGAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Configuracion RAG actualizada exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
