using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ConsultasPlantillasController : Controller
{
    private readonly IApiService _apiService;

    public ConsultasPlantillasController(IApiService apiService)
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
            var plantillas = await _apiService.GetConsultasPlantillasAsync(GetCurrentUserId(), GetIpAddress());
            return View(plantillas);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar plantillas: {ex.Message}";
            return View(new List<ConsultaPlantillaDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
        ViewBag.Conexiones = conexiones;
        return View(new CrearConsultaPlantillaRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearConsultaPlantillaRequest model)
    {
        if (!ModelState.IsValid)
        {
            var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Conexiones = conexiones;
            return View(model);
        }

        try
        {
            await _apiService.CrearConsultaPlantillaAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Plantilla de consulta creada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Conexiones = conexiones;
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var plantilla = await _apiService.GetConsultaPlantillaByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (plantilla == null) return NotFound();

            var request = new ActualizarConsultaPlantillaRequest
            {
                Nombre = plantilla.Nombre,
                Descripcion = plantilla.Descripcion,
                IdConexion = plantilla.IdConexion,
                ConsultaSql = plantilla.ConsultaSql,
                Parametros = plantilla.Parametros,
                Activa = plantilla.Activa
            };

            var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Conexiones = conexiones;
            ViewBag.PlantillaId = id;
            return View(request);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar plantilla: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarConsultaPlantillaRequest model)
    {
        if (!ModelState.IsValid)
        {
            var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Conexiones = conexiones;
            ViewBag.PlantillaId = id;
            return View(model);
        }

        try
        {
            await _apiService.ActualizarConsultaPlantillaAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Plantilla actualizada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var conexiones = await _apiService.GetConexionesBaseDatosActivasAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Conexiones = conexiones;
            ViewBag.PlantillaId = id;
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try
        {
            await _apiService.ActivarConsultaPlantillaAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Plantilla activada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try
        {
            await _apiService.DesactivarConsultaPlantillaAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Plantilla desactivada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await _apiService.EliminarConsultaPlantillaAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Plantilla eliminada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
