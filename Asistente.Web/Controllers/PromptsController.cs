using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class PromptsController : Controller
{
    private readonly IApiService _apiService;

    public PromptsController(IApiService apiService)
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

    public async Task<IActionResult> Index(int? asistenteId)
    {
        try
        {
            var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Asistentes = asistentes;

            List<PromptSistemaDto> prompts;
            if (asistenteId.HasValue)
            {
                prompts = (await _apiService.GetPromptsByAsistenteIdAsync(asistenteId.Value, GetCurrentUserId(), GetIpAddress())).ToList();
                ViewBag.AsistenteSeleccionado = asistentes.FirstOrDefault(a => a.IdAsistente == asistenteId.Value);
            }
            else
            {
                prompts = (await _apiService.GetPromptsAsync(GetCurrentUserId(), GetIpAddress())).ToList();
            }

            return View(prompts);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar prompts: {ex.Message}";
            return View(new List<PromptSistemaDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int? asistenteId)
    {
        var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
        var activos = asistentes.Where(a => a.Activo).ToList();

        if (!activos.Any())
        {
            TempData["ErrorMessage"] = "Debe crear al menos un asistente activo antes de crear un prompt.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Asistentes = activos;
        var model = new CrearPromptRequest();
        if (asistenteId.HasValue)
            model.IdAsistente = asistenteId.Value;
        else
            model.IdAsistente = activos.First().IdAsistente;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearPromptRequest model)
    {
        if (!ModelState.IsValid)
        {
            var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Asistentes = asistentes.Where(a => a.Activo).ToList();
            return View(model);
        }

        try
        {
            model.UsuarioCreacion = User.Identity?.Name ?? "Sistema";
            await _apiService.CrearPromptAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Prompt creado exitosamente.";
            return RedirectToAction(nameof(Index), new { asistenteId = model.IdAsistente });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Asistentes = asistentes.Where(a => a.Activo).ToList();
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var prompt = await _apiService.GetPromptByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (prompt == null)
                return NotFound();

            var model = new ActualizarPromptRequest
            {
                Nombre = prompt.Nombre,
                Contenido = prompt.Contenido,
                Activo = prompt.Activo
            };

            ViewBag.PromptId = id;
            ViewBag.PromptNombre = prompt.Nombre;
            ViewBag.Version = prompt.Version;
            ViewBag.IdAsistente = prompt.IdAsistente;

            return View(model);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar prompt: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarPromptRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.PromptId = id;
            return View(model);
        }

        try
        {
            model.Activo = true;
            var result = await _apiService.ActualizarPromptAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = $"Prompt actualizado. Nueva versión: {result.Version}";
            return RedirectToAction(nameof(Historial), new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.PromptId = id;
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Historial(int id)
    {
        try
        {
            var prompt = await _apiService.GetPromptByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (prompt == null)
                return NotFound();

            var historial = await _apiService.GetHistorialPromptAsync(id, GetCurrentUserId(), GetIpAddress());

            ViewBag.PromptId = id;
            ViewBag.PromptNombre = prompt.Nombre;
            ViewBag.VersionActual = prompt.Version;
            ViewBag.IdAsistente = prompt.IdAsistente;

            return View(historial);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar historial: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Prueba(int? asistenteId)
    {
        try
        {
            var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Asistentes = asistentes.Where(a => a.Activo).ToList();

            if (asistenteId.HasValue)
            {
                var prompts = await _apiService.GetPromptsByAsistenteIdAsync(asistenteId.Value, GetCurrentUserId(), GetIpAddress());
                ViewBag.Prompts = prompts;
                ViewBag.AsistenteSeleccionado = asistentes.FirstOrDefault(a => a.IdAsistente == asistenteId.Value);
            }
            else
            {
                ViewBag.Prompts = new List<PromptSistemaDto>();
            }

            return View(new PruebaAsistenteRequest { IdAsistente = asistenteId ?? 0 });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return View(new PruebaAsistenteRequest { IdAsistente = asistenteId ?? 0 });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Prueba(PruebaAsistenteRequest model)
    {
        var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
        ViewBag.Asistentes = asistentes.Where(a => a.Activo).ToList();

        if (model.IdAsistente > 0)
        {
            var prompts = await _apiService.GetPromptsByAsistenteIdAsync(model.IdAsistente, GetCurrentUserId(), GetIpAddress());
            ViewBag.Prompts = prompts;
            ViewBag.AsistenteSeleccionado = asistentes.FirstOrDefault(a => a.IdAsistente == model.IdAsistente);
        }

        if (string.IsNullOrWhiteSpace(model.Mensaje))
        {
            ModelState.AddModelError(string.Empty, "Escriba un mensaje para probar.");
            return View(model);
        }

        try
        {
            var result = await _apiService.ProbarAsistenteAsync(model, GetCurrentUserId(), GetIpAddress());
            ViewBag.Resultado = result;
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicar(int id)
    {
        try
        {
            await _apiService.DuplicarPromptAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Prompt duplicado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try
        {
            await _apiService.ActivarPromptAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Prompt activado.";
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
            await _apiService.DesactivarPromptAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Prompt desactivado.";
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
            await _apiService.EliminarPromptAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Prompt eliminado.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restaurar(int id, int idHistorial)
    {
        try
        {
            await _apiService.RestaurarPromptDesdeHistorialAsync(id, idHistorial, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Versión restaurada exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Historial), new { id });
    }
}
