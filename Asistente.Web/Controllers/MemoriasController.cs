using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Asistente.Web.Controllers;

/// <summary>Dashboard de Memoria Empresarial (ETAPA 21).</summary>
[Authorize(Roles = "Administrador")]
public class MemoriasController : Controller
{
    private readonly IApiService _apiService;
    private readonly ILogger<MemoriasController> _logger;

    public MemoriasController(IApiService apiService, ILogger<MemoriasController> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    private int UsuarioId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out var id) ? id : 1;
    }

    private string Ip() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index(string? ambito, string? q)
    {
        try
        {
            ViewBag.Metricas = await _apiService.GetMemoriaMetricasAsync(UsuarioId(), Ip());
            if (!string.IsNullOrWhiteSpace(q))
                ViewBag.Busqueda = await _apiService.BuscarMemoriasAsync(q, UsuarioId(), Ip());
            var memorias = await _apiService.GetMemoriasAsync(ambito, UsuarioId(), Ip());
            ViewBag.Ambito = ambito;
            ViewBag.Q = q;
            return View(memorias);
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar memorias: {ex.Message}";
            return View(new System.Collections.Generic.List<MemoryDto>());
        }
    }

    public async Task<IActionResult> Detalle(string id)
    {
        var memoria = await _apiService.GetMemoriaAsync(id, UsuarioId(), Ip());
        if (memoria == null)
        {
            TempData["ErrorMessage"] = $"No existe o no tienes permiso para '{id}'.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            ViewBag.Versiones = await _apiService.GetMemoriaVersionesAsync(id, UsuarioId(), Ip());
            ViewBag.Auditoria = await _apiService.GetMemoriaAuditoriaAsync(id, UsuarioId(), Ip());
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar detalle: {ex.Message}";
        }
        return View(memoria);
    }

    [HttpGet]
    public IActionResult Crear() => View(new CrearMemoryRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearMemoryRequest request)
    {
        try
        {
            var creada = await _apiService.CrearMemoriaAsync(request, UsuarioId(), Ip());
            TempData["OkMessage"] = $"Memoria '{creada.Codigo}' creada (v1).";
            return RedirectToAction(nameof(Detalle), new { id = creada.Codigo });
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al crear: {ex.Message}";
            return View(request);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(string id, string contenido, string? motivo)
    {
        try
        {
            await _apiService.ActualizarMemoriaAsync(id, contenido, motivo, UsuarioId(), Ip());
            TempData["OkMessage"] = "Memoria actualizada (nueva versión).";
        }
        catch (System.Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revertir(string id, int numero, string? motivo)
    {
        try
        {
            await _apiService.RevertirMemoriaAsync(id, numero, motivo, UsuarioId(), Ip());
            TempData["OkMessage"] = $"Revertida a v{numero} (nueva versión, sin reescribir historia).";
        }
        catch (System.Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(string id)
    {
        try
        {
            await _apiService.EliminarMemoriaAsync(id, UsuarioId(), Ip());
            TempData["OkMessage"] = "Memoria eliminada (o desactivada según trazabilidad).";
        }
        catch (System.Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Retencion()
    {
        try
        {
            ViewBag.Pendientes = await _apiService.GetMemoriaRetencionPendientesAsync(UsuarioId(), Ip());
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AplicarRetencion()
    {
        try
        {
            var (des, eli) = await _apiService.AplicarRetencionAsync(UsuarioId(), Ip());
            TempData["OkMessage"] = $"Retención aplicada: {des} desactivadas, {eli} eliminadas.";
        }
        catch (System.Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
        return RedirectToAction(nameof(Retencion));
    }
}
