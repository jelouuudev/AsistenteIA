using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class FuentesConocimientoController : Controller
{
    private readonly IApiService _apiService;

    public FuentesConocimientoController(IApiService apiService)
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
            var fuentes = await _apiService.GetFuentesConocimientoAsync(GetCurrentUserId(), GetIpAddress());
            return View(fuentes);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar fuentes: {ex.Message}";
            return View(new List<FuenteConocimientoDto>());
        }
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View(new CrearFuenteConocimientoRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearFuenteConocimientoRequest model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await _apiService.CrearFuenteConocimientoAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Fuente de conocimiento creada exitosamente.";
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
            var fuente = await _apiService.GetFuenteConocimientoByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (fuente == null) return NotFound();

            var request = new ActualizarFuenteConocimientoRequest
            {
                Nombre = fuente.Nombre,
                Codigo = fuente.Codigo,
                Descripcion = fuente.Descripcion,
                Tipo = fuente.Tipo,
                Activo = fuente.Activo,
                Prioridad = fuente.Prioridad
            };

            ViewBag.FuenteId = id;
            return View(request);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar fuente: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarFuenteConocimientoRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.FuenteId = id;
            return View(model);
        }

        try
        {
            await _apiService.ActualizarFuenteConocimientoAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Fuente actualizada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.FuenteId = id;
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try
        {
            await _apiService.ActivarFuenteConocimientoAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Fuente activada.";
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
            await _apiService.DesactivarFuenteConocimientoAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Fuente desactivada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Documentos(int id)
    {
        try
        {
            var fuente = await _apiService.GetFuenteConocimientoByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (fuente == null) return NotFound();

            var documentos = await _apiService.GetDocumentosDeFuenteAsync(id, GetCurrentUserId(), GetIpAddress());
            ViewBag.FuenteId = id;
            ViewBag.FuenteNombre = fuente.Nombre;
            return View(documentos);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarDocumento(int idFuente, int idDocumento)
    {
        try
        {
            await _apiService.AsignarDocumentoAFuenteAsync(new AsignarDocumentoAFuenteRequest
            {
                IdDocumento = idDocumento,
                IdFuente = idFuente
            }, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento asignado a la fuente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Documentos), new { id = idFuente });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesasignarDocumento(int idFuente, int idDocumento)
    {
        try
        {
            await _apiService.DesasignarDocumentoDeFuenteAsync(idDocumento, idFuente, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento desasignado de la fuente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Documentos), new { id = idFuente });
    }

    public async Task<IActionResult> Asistentes(int id)
    {
        try
        {
            var fuente = await _apiService.GetFuenteConocimientoByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (fuente == null) return NotFound();

            var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
            var asistentesDeFuente = await _apiService.GetAsistentesDeFuenteAsync(id, GetCurrentUserId(), GetIpAddress());

            ViewBag.FuenteId = id;
            ViewBag.FuenteNombre = fuente.Nombre;
            ViewBag.FuentesAsistente = asistentesDeFuente;
            return View(asistentes);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarAsistente(int idFuente, int idAsistente)
    {
        try
        {
            await _apiService.AsignarFuenteAAsistenteAsync(new AsignarFuenteAAsistenteRequest
            {
                IdAsistente = idAsistente,
                IdFuente = idFuente
            }, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Fuente asignada al asistente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Asistentes), new { id = idFuente });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesasignarAsistente(int idFuente, int idAsistente)
    {
        try
        {
            await _apiService.DesasignarFuenteDeAsistenteAsync(idAsistente, idFuente, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Fuente desasignada del asistente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Asistentes), new { id = idFuente });
    }

    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var dashboard = await _apiService.GetDashboardFuentesAsync(GetCurrentUserId(), GetIpAddress());
            return View(dashboard);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return View(new DashboardFuentesDto());
        }
    }
}
