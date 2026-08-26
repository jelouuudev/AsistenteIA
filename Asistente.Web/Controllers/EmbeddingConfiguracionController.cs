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
public class EmbeddingConfiguracionController : Controller
{
    private readonly IApiService _apiService;

    public EmbeddingConfiguracionController(IApiService apiService)
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

            var configuraciones = await _apiService.GetEmbeddingConfiguracionesAsync(userId, ip);
            var activa = await _apiService.GetEmbeddingConfiguracionActivaAsync(userId, ip);

            ViewBag.ConfiguracionActiva = activa;

            return View(configuraciones);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar configuraciones: {ex.Message}";
            return View(new List<EmbeddingConfiguracionDto>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(int id, ActualizarEmbeddingConfiguracionRequest request)
    {
        try
        {
            await _apiService.ActualizarEmbeddingConfiguracionAsync(id, request, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Configuración actualizada exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al actualizar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ActualizarEmbeddingConfiguracionRequest request)
    {
        try
        {
            await _apiService.CrearEmbeddingConfiguracionAsync(request, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Configuración creada exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al crear: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
