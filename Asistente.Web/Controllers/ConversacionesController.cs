using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Operador,Supervisor")]
public class ConversacionesController : Controller
{
    private readonly IApiService _apiService;

    public ConversacionesController(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<IActionResult> Index(string? q, bool? mostrarArchivadas)
    {
        var (userId, ip) = GetUserInfo();
        IEnumerable<ConversacionListDto> conversaciones;
        try
        {
            if (!string.IsNullOrWhiteSpace(q))
                conversaciones = await _apiService.BuscarConversacionesAsync(q, userId, ip);
            else
                conversaciones = await _apiService.GetConversacionesAsync(userId, ip);

            // Por defecto, ocultar conversaciones archivadas
            if (mostrarArchivadas != true)
                conversaciones = conversaciones.Where(c => c.Estado != "Archivada");
        }
        catch
        {
            conversaciones = Enumerable.Empty<ConversacionListDto>();
        }

        ViewBag.SearchQuery = q;
        ViewBag.MostrarArchivadas = mostrarArchivadas == true;
        return View(conversaciones);
    }

    [HttpPost]
    public async Task<IActionResult> Renombrar(int id, string titulo)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            await _apiService.RenombrarConversacionAsync(id, titulo, userId, ip);
            TempData["SuccessMessage"] = "Conversación renombrada exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al renombrar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Archivar(int id)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            await _apiService.ArchivarConversacionAsync(id, userId, ip);
            TempData["SuccessMessage"] = "Conversación archivada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al archivar: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            await _apiService.EliminarConversacionAsync(id, userId, ip);
            TempData["SuccessMessage"] = "Conversación eliminada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al eliminar: {ex.Message}";
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
