using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class AsistentesController : Controller
{
    private readonly IApiService _apiService;

    public AsistentesController(IApiService apiService)
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
            var asistentes = await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress());
            return View(asistentes);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar asistentes: {ex.Message}";
            return View(new List<AsistenteDto>());
        }
    }

    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var asistentes = (await _apiService.GetAsistentesAsync(GetCurrentUserId(), GetIpAddress())).ToList();

            var activos = asistentes.Count(a => a.Estado == Asistente.Domain.Entities.EstadoAgente.Activo);
            var inactivos = asistentes.Count(a => a.Estado == Asistente.Domain.Entities.EstadoAgente.Inactivo);
            var total = asistentes.Count;
            var totalVersiones = asistentes.Sum(a => a.Version);

            ViewBag.Estadisticas = new
            {
                Total = total,
                Activos = activos,
                Inactivos = inactivos,
                TotalVersiones = totalVersiones
            };

            return View(asistentes);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar dashboard: {ex.Message}";
            ViewBag.Estadisticas = new { Total = 0, Publicados = 0, EnPrueba = 0, Borradores = 0, Deshabilitados = 0, Activos = 0, TotalVersiones = 0 };
            return View(new List<AsistenteDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        await CargarCatalogosAsync();
        return View(new CrearAsistenteRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearAsistenteRequest model)
    {
        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync();
            return View(model);
        }

        try
        {
            await _apiService.CrearAsistenteAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Agente creado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await CargarCatalogosAsync();
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var asistente = await _apiService.GetAsistenteByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (asistente == null)
                return NotFound();

            var request = new ActualizarAsistenteRequest
            {
                Codigo = asistente.Codigo,
                Nombre = asistente.Nombre,
                Descripcion = asistente.Descripcion,
                Objetivo = asistente.Objetivo,
                PromptSistema = asistente.PromptSistema,
                ModeloIA = asistente.ModeloIA,
                Activo = asistente.Activo,
                Idioma = asistente.Idioma,
                LongitudMaximaRespuesta = asistente.LongitudMaximaRespuesta,
                NivelFormalidad = asistente.NivelFormalidad,
                FormatoRespuesta = asistente.FormatoRespuesta,
                Restricciones = asistente.Restricciones,
                MensajeBienvenida = asistente.MensajeBienvenida,
                Temperatura = asistente.Temperatura,
                MaxTokens = asistente.MaxTokens,
                TimeoutSegundos = asistente.TimeoutSegundos,
                Fuentes = asistente.Fuentes,
                Herramientas = asistente.Herramientas,
                Workflows = asistente.Workflows,
                Roles = asistente.Roles,
                Usuarios = asistente.Usuarios
            };

            ViewBag.AsistenteId = id;
            ViewBag.AsistenteNombre = asistente.Nombre;
            await CargarCatalogosAsync();

            return View(request);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar asistente: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task CargarCatalogosAsync()
    {
        try
        {
            ViewBag.Roles = (await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress())).ToList();
            ViewBag.Usuarios = (await _apiService.GetUsuariosAsync(GetCurrentUserId(), GetIpAddress())).ToList();
            ViewBag.Fuentes = (await _apiService.GetFuentesConocimientoAsync(GetCurrentUserId(), GetIpAddress())).ToList();
            ViewBag.Herramientas = (await _apiService.GetHerramientasAsync(null, GetCurrentUserId(), GetIpAddress())).ToList();
            ViewBag.Workflows = (await _apiService.GetWorkflowsAsync(GetCurrentUserId(), GetIpAddress())).ToList();
        }
        catch
        {
            ViewBag.Roles = new List<RolDto>();
            ViewBag.Usuarios = new List<UsuarioDto>();
            ViewBag.Fuentes = new List<FuenteConocimientoDto>();
            ViewBag.Herramientas = new List<HerramientaDto>();
            ViewBag.Workflows = new List<WorkflowDto>();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarAsistenteRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.AsistenteId = id;
            return View(model);
        }

        try
        {
            await _apiService.ActualizarAsistenteAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Asistente actualizado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.AsistenteId = id;
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try
        {
            await _apiService.ActivarAsistenteAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Asistente activado.";
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
            await _apiService.DesactivarAsistenteAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Asistente desactivado.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publicar(int id)
    {
        try
        {
            await _apiService.PublicarAgenteAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Agente publicado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarPrueba(int id)
    {
        try
        {
            await _apiService.EnviarAgenteAPruebaAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Agente enviado a prueba.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicar(int id)
    {
        try
        {
            await _apiService.DuplicarAgenteAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Agente duplicado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Versiones(int id)
    {
        try
        {
            var versiones = await _apiService.GetVersionesAgenteAsync(id, GetCurrentUserId(), GetIpAddress());
            ViewBag.AsistenteId = id;
            var asistente = await _apiService.GetAsistenteByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            ViewBag.AsistenteNombre = asistente?.Nombre ?? "Agente";
            return View(versiones);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar versiones: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearVersion(int id)
    {
        try
        {
            await _apiService.CrearVersionAgenteAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Nueva versión del agente creada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Versiones), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestaurarVersion(int id, int idVersion)
    {
        try
        {
            await _apiService.RestaurarVersionAgenteAsync(id, idVersion, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = $"Versión restaurada exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Versiones), new { id });
    }
}
