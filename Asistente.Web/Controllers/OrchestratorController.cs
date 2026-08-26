using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Operador,Supervisor")]
public class OrchestratorController : Controller
{
    private readonly IApiService _apiService;

    public OrchestratorController(IApiService apiService) => _apiService = apiService;

    private (int userId, string ip) GetUserInfo()
        => (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");

    /// <summary>Pantalla principal: ejecutar solicitud orquestada, dashboard y trazas.</summary>
    public async Task<IActionResult> Index()
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            ViewBag.Agentes = await _apiService.GetAgentesParaOrquestadorAsync(userId, ip);
            ViewBag.Dashboard = await _apiService.GetOrchestratorDashboardAsync(userId, ip);
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
            ViewBag.Agentes = new List<AgenteSimpleDto>();
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ejecutar(int idAgentePrincipal, string pregunta)
    {
        var (userId, ip) = GetUserInfo();
        if (idAgentePrincipal <= 0 || string.IsNullOrWhiteSpace(pregunta))
        {
            TempData["ErrorMessage"] = "Seleccione un agente principal y escriba la solicitud.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var resultado = await _apiService.ExecuteOrchestratorAsync(idAgentePrincipal, pregunta, userId, ip);
            if (resultado?.IdExecution > 0)
                return RedirectToAction(nameof(Trazas), new { id = resultado.IdExecution });
            TempData["ErrorMessage"] = "El orquestador no devolvió un identificador de ejecución.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>Visualizador de trazas de una ejecución.</summary>
    [HttpGet]
    public async Task<IActionResult> Trazas(int id)
    {
        var (userId, ip) = GetUserInfo();
        ViewBag.Trazas = await _apiService.GetOrchestratorTrazasAsync(id, userId, ip);
        ViewBag.IdExecution = id;
        return View();
    }

    // ===== Administración de reglas de colaboración (Actividad 3) =====
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Reglas()
    {
        var (userId, ip) = GetUserInfo();
        ViewBag.Reglas = await _apiService.GetReglasColaboracionAsync(userId, ip);
        ViewBag.Agentes = await _apiService.GetAgentesParaOrquestadorAsync(userId, ip);
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearRegla(int agenteOrigen, int agenteDestino, bool permitido, int prioridad, bool activa)
    {
        var (userId, ip) = GetUserInfo();
        await _apiService.CrearReglaColaboracionAsync(new AgentCollaborationRuleDto
        {
            AgenteOrigen = agenteOrigen,
            AgenteDestino = agenteDestino,
            Permitido = permitido,
            Prioridad = prioridad,
            Activa = activa
        }, userId, ip);
        TempData["SuccessMessage"] = "Regla de colaboración creada.";
        return RedirectToAction(nameof(Reglas));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarRegla(int id)
    {
        var (userId, ip) = GetUserInfo();
        await _apiService.EliminarReglaColaboracionAsync(id, userId, ip);
        TempData["SuccessMessage"] = "Regla eliminada.";
        return RedirectToAction(nameof(Reglas));
    }
}
