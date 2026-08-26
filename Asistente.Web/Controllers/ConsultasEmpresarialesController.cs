using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ConsultasEmpresarialesController : Controller
{
    private readonly IApiService _apiService;

    public ConsultasEmpresarialesController(IApiService apiService)
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
            var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
            var plantillas = await _apiService.GetConsultasPlantillasAsync(GetCurrentUserId(), GetIpAddress());
            var dashboard = await _apiService.GetDashboardConsultasAsync(GetCurrentUserId(), GetIpAddress());

            ViewBag.Conexiones = conexiones;
            ViewBag.Plantillas = plantillas;
            return View(dashboard);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar el motor de consultas: {ex.Message}";
            return View(new DashboardConsultasDto());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcesarPregunta(string pregunta)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
        {
            TempData["ErrorMessage"] = "Escriba una pregunta.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var resultado = await _apiService.ProcesarPreguntaEmpresarialAsync(
                new ProcesarPreguntaRequest { Pregunta = pregunta }, GetCurrentUserId(), GetIpAddress());

            TempData["ConsultaResultado"] = resultado.Respuesta;
            TempData["ConsultaSql"] = resultado.ConsultaSql;
            TempData["ConsultaTipo"] = resultado.Tipo;
            TempData["ConsultaError"] = resultado.Error;
            TempData["ConsultaRegistros"] = resultado.CantidadRegistros?.ToString();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ejecutar(int idConexion, int? idPlantilla, string consultaSql)
    {
        try
        {
            if (idPlantilla.HasValue)
            {
                var plantilla = await _apiService.GetConsultaPlantillaByIdAsync(idPlantilla.Value, GetCurrentUserId(), GetIpAddress());
                consultaSql = plantilla?.ConsultaSql ?? consultaSql;
            }

            if (string.IsNullOrWhiteSpace(consultaSql))
            {
                TempData["ErrorMessage"] = "Debe proporcionar una consulta SQL o seleccionar una plantilla.";
                return RedirectToAction(nameof(Index));
            }

            var resultado = await _apiService.EjecutarConsultaEmpresarialAsync(
                new EjecutarConsultaRequest
                {
                    IdConexion = idConexion,
                    IdPlantilla = idPlantilla,
                    ConsultaSql = consultaSql,
                    Pregunta = consultaSql
                }, GetCurrentUserId(), GetIpAddress());

            TempData["ConsultaResultado"] = resultado.Exitoso
                ? $"Consulta ejecutada: {resultado.CantidadRegistros} registro(s) en {resultado.TiempoEjecucionMs} ms."
                : resultado.Error;
            TempData["ConsultaSql"] = consultaSql;
            TempData["ConsultaTipo"] = resultado.Estado;
            TempData["ConsultaError"] = resultado.Exitoso ? null : resultado.Error;
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Historial()
    {
        try
        {
            var consultas = await _apiService.GetConsultasEjecutadasAsync(GetCurrentUserId(), GetIpAddress());
            return View(consultas);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar historial: {ex.Message}";
            return View(new List<ConsultaEjecutadaDto>());
        }
    }
}
