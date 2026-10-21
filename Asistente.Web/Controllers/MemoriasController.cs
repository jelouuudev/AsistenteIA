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
// Sin rol fijo aquí: cada acción declara el suyo. [Authorize] se combina en AND,
// así que si el controlador exigiera Administrador a nivel de clase ninguna acción
// podría abrirse para Supervisor (lección ya aplicada en SeguridadController).
// Las acciones que modifican memoria siguen siendo de Administrador.
[Authorize]
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

    /// <summary>
    /// Panel de memoria (ETAPA 21, sección 17): métricas, auditoría reciente y
    /// retención pendiente. De lectura, así que lo ve también Supervisor, que ya
    /// tiene AUDITORIA_CONSULTAR. Crear o modificar memorias sigue siendo de
    /// Administrador.
    /// </summary>
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            return View(await _apiService.GetMemoriaDashboardAsync(UsuarioId(), Ip()));
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar el panel: {ex.Message}";
            return View(new DashboardMemoriaDto());
        }
    }

    [Authorize(Roles = "Administrador")]
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

    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
    public IActionResult Crear() => View(new CrearMemoryRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Crear(CrearMemoryRequest request)
    {
        try
        {
            // La UI crea siempre memorias abiertas: el acceso lo decide el ambito
            // (Usuario, Agente y Workflow solo la ve su dueno; Organizacion es
            // compartida) y escribir en los ambitos compartidos ya exige el
            // permiso MEMORIA_ADMINISTRAR. El permiso por codigo (MEMORIA:{codigo})
            // sigue existiendo en la API para uso programatico, pero no se ofrece
            // en el formulario porque no hay donde concederlo: una memoria creada
            // con esa exigencia quedaria invisible e indestructible para todos.
            request.RequierePermiso = false;
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
    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
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
    [Authorize(Roles = "Administrador")]
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

    // Solo lectura de lo que la política tocaría: Supervisor ya ve la auditoría,
    // y sin esto no podría comprobar si la retención tiene pendientes.
    [Authorize(Roles = "Administrador,Supervisor")]
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
    [Authorize(Roles = "Administrador")]
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
