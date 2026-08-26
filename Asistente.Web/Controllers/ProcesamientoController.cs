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
public class ProcesamientoController : Controller
{
    private readonly IApiService _apiService;

    public ProcesamientoController(IApiService apiService)
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

    public async Task<IActionResult> Index(string? estado)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ip = GetIpAddress();

            IEnumerable<DocumentoProcesadoDto> documentos;

            if (!string.IsNullOrEmpty(estado))
            {
                documentos = await _apiService.GetProcesamientoByEstadoAsync(estado, userId, ip);
            }
            else
            {
                documentos = await _apiService.GetProcesamientoAllAsync(userId, ip);
            }

            var dashboard = await _apiService.GetProcesamientoDashboardAsync(userId, ip);

            ViewBag.Dashboard = dashboard;
            ViewBag.EstadoFiltro = estado;

            return View(documentos);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar procesamiento: {ex.Message}";
            return View(new List<DocumentoProcesadoDto>());
        }
    }

    public async Task<IActionResult> Detalle(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ip = GetIpAddress();

            var procesado = await _apiService.GetProcesamientoByIdAsync(id, userId, ip);
            if (procesado == null)
            {
                return NotFound();
            }

            var chunks = await _apiService.GetChunksAsync(id, userId, ip);

            ViewBag.Chunks = chunks;

            return View(procesado);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar detalle: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Procesar(int versionId)
    {
        try
        {
            await _apiService.ProcesarDocumentoAsync(versionId, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento procesado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al procesar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprocesar(int id)
    {
        try
        {
            await _apiService.ReprocesarDocumentoAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento reprocesado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al reprocesar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
