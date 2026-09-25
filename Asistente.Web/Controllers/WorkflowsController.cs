using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class WorkflowsController : Controller
{
    private readonly IApiService _apiService;

    public WorkflowsController(IApiService apiService)
    {
        _apiService = apiService;
    }

    private int GetCurrentUserId()
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;

    private string GetIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        try
        {
            var workflows = await _apiService.GetWorkflowsAsync(GetCurrentUserId(), GetIpAddress());
            // Reglas agrupadas por flujo para mostrar qué evento invoca cada workflow.
            // Si falla la carga de reglas, la tabla igual se muestra (columna con "—").
            Dictionary<int, List<ReglaEventoDto>> porWorkflow = new();
            try
            {
                var reglas = await _apiService.GetReglasEventoAsync(GetCurrentUserId(), GetIpAddress());
                porWorkflow = reglas
                    .GroupBy(r => r.IdWorkflow)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            catch { /* la columna mostrará "—" */ }
            ViewBag.ReglasPorWorkflow = porWorkflow;
            return View(workflows);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar flujos de trabajo: {ex.Message}";
            ViewBag.ReglasPorWorkflow = new Dictionary<int, List<ReglaEventoDto>>();
            return View(new List<WorkflowDto>());
        }
    }

    [HttpGet]
    public IActionResult Crear()
        => View(new CrearWorkflowRequest
        {
            Nombre = "",
            Codigo = "",
            Descripcion = "",
            Disparadores = "",
            Pasos = new List<WorkflowPasoRequest>
            {
                new() { Orden = 1, Nombre = "", Herramienta = "SqlQueryTool", Parametros = "{}", RequiereConfirmacion = false, ReintentosMaximos = 1, TiempoMaximoMs = 60000, EstrategiaError = "Cancelar" }
            }
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearWorkflowRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await _apiService.CrearWorkflowAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Flujo de trabajo registrado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var wf = await _apiService.GetWorkflowByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (wf == null) return NotFound();
            var model = new ActualizarWorkflowRequest
            {
                Nombre = wf.Nombre,
                Codigo = wf.Codigo,
                Descripcion = wf.Descripcion,
                Disparadores = wf.Disparadores,
                Pasos = wf.Pasos.Select(p => new WorkflowPasoRequest
                {
                    Orden = p.Orden,
                    Nombre = p.Nombre,
                    Herramienta = p.Herramienta,
                    Parametros = p.Parametros,
                    RequiereConfirmacion = p.RequiereConfirmacion,
                    ReintentosMaximos = p.ReintentosMaximos,
                    TiempoMaximoMs = p.TiempoMaximoMs,
                    EstrategiaError = p.EstrategiaError
                }).ToList()
            };
            ViewBag.IdWorkflow = wf.IdWorkflow;
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarWorkflowRequest model)
    {
        if (!ModelState.IsValid) { ViewBag.IdWorkflow = id; return View(model); }
        try
        {
            await _apiService.ActualizarWorkflowAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Flujo de trabajo actualizado.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.IdWorkflow = id;
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try { await _apiService.ActivarWorkflowAsync(id, GetCurrentUserId(), GetIpAddress()); TempData["SuccessMessage"] = "Flujo de trabajo activado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try { await _apiService.DesactivarWorkflowAsync(id, GetCurrentUserId(), GetIpAddress()); TempData["SuccessMessage"] = "Flujo de trabajo suspendido."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Versionar(int id)
    {
        try { await _apiService.VersionarWorkflowAsync(id, GetCurrentUserId(), GetIpAddress()); TempData["SuccessMessage"] = "Nueva versión del flujo creada."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        try { await _apiService.EliminarWorkflowAsync(id, GetCurrentUserId(), GetIpAddress()); TempData["SuccessMessage"] = "Flujo de trabajo eliminado."; }
        catch (Exception ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Configuracion()
    {
        try
        {
            var config = await _apiService.GetConfiguracionWorkflowAsync(GetCurrentUserId(), GetIpAddress());
            return View(config);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return View(new ConfiguracionWorkflowDto());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configuracion(ConfiguracionWorkflowDto model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await _apiService.GuardarConfiguracionWorkflowAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Configuración del motor de flujos guardada.";
            return RedirectToAction(nameof(Configuracion));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
