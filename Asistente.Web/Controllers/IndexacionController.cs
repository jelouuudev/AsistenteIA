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
public class IndexacionController : Controller
{
    private readonly IApiService _apiService;

    public IndexacionController(IApiService apiService)
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

            IEnumerable<DocumentoIndexadoDto> documentos;

            if (!string.IsNullOrEmpty(estado))
            {
                documentos = await _apiService.GetIndexacionByEstadoAsync(estado, userId, ip);
            }
            else
            {
                documentos = await _apiService.GetIndexacionAllAsync(userId, ip);
            }

            var dashboard = await _apiService.GetIndexacionDashboardAsync(userId, ip);

            ViewBag.Dashboard = dashboard;
            ViewBag.EstadoFiltro = estado;

            return View(documentos);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar indexación: {ex.Message}";
            return View(new List<DocumentoIndexadoDto>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Indexar(int documentoProcesadoId)
    {
        try
        {
            await _apiService.IndexarDocumentoAsync(documentoProcesadoId, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento indexado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al indexar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reindexar(int documentoProcesadoId)
    {
        try
        {
            await _apiService.ReindexarDocumentoAsync(documentoProcesadoId, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento reindexado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al reindexar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReindexarTodos()
    {
        try
        {
            await _apiService.ReindexarTodosAsync(GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Todos los documentos reindexados exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al reindexar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarIndice(int documentoProcesadoId)
    {
        try
        {
            await _apiService.EliminarIndiceAsync(documentoProcesadoId, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Índice eliminado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al eliminar índice: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
