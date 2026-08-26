using System.Security.Claims;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ConexionesBaseDatosController : Controller
{
    private readonly IApiService _apiService;
    private readonly ILogger<ConexionesBaseDatosController> _logger;

    public ConexionesBaseDatosController(
        IApiService apiService,
        ILogger<ConexionesBaseDatosController> logger)
    {
        _apiService = apiService;
        _logger = logger;
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
            var conexiones = await _apiService.GetConexionesBaseDatosAsync(GetCurrentUserId(), GetIpAddress());
            return View(conexiones);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar conexiones: {ex.Message}";
            return View(new List<ConexionBaseDatosDto>());
        }
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View(new CrearConexionBaseDatosRequest { AutenticacionWindows = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearConexionBaseDatosRequest model)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Intento de registrar una conexión rechazado por validación del formulario.");
            return View(model);
        }

        try
        {
            _logger.LogInformation(
                "Registrando conexión '{Nombre}' para {Servidor}/{BaseDatos}.",
                model.Nombre, model.Servidor, model.BaseDatos);

            await _apiService.CrearConexionBaseDatosAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Conexión a base de datos registrada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "No se pudo registrar la conexión '{Nombre}' para {Servidor}/{BaseDatos}.",
                model.Nombre, model.Servidor, model.BaseDatos);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Probar(CrearConexionBaseDatosRequest model)
    {
        var resultado = await _apiService.ProbarConexionBaseDatosAsync(new ProbarConexionRequest
        {
            Servidor = model.Servidor,
            BaseDatos = model.BaseDatos,
            UsuarioConexion = model.UsuarioConexion,
            Contrasena = model.Contrasena,
            AutenticacionWindows = model.AutenticacionWindows
        }, GetCurrentUserId(), GetIpAddress());

        TempData["SuccessMessage"] = resultado.Exitoso ? resultado.Mensaje : null;
        TempData["ErrorMessage"] = resultado.Exitoso ? null : resultado.Mensaje;
        return RedirectToAction(nameof(Crear));
    }

    [HttpGet]
    public async Task<IActionResult> Esquema(int id)
    {
        try
        {
            var conexion = await _apiService.GetConexionBaseDatosByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (conexion == null) return NotFound();

            var esquema = await _apiService.DescubrirEsquemaAsync(id, GetCurrentUserId(), GetIpAddress());
            var tablas = await _apiService.GetTablasAutorizadasAsync(id, GetCurrentUserId(), GetIpAddress());
            var vistas = await _apiService.GetVistasAutorizadasAsync(id, GetCurrentUserId(), GetIpAddress());

            ViewBag.ConexionId = id;
            ViewBag.ConexionNombre = conexion.Nombre;
            ViewBag.TablasAutorizadas = tablas;
            ViewBag.VistasAutorizadas = vistas;
            return View(esquema);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al descubrir esquema: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarTabla(int idConexion, string nombreTabla, string esquema, string descripcion)
    {
        try
        {
            await _apiService.AgregarTablaAutorizadaAsync(idConexion, new TablaAutorizadaRequest
            {
                NombreTabla = nombreTabla,
                Esquema = string.IsNullOrWhiteSpace(esquema) ? "dbo" : esquema,
                Descripcion = descripcion
            }, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = $"Tabla '{nombreTabla}' autorizada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Esquema), new { id = idConexion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarVista(int idConexion, string nombreVista, string descripcion)
    {
        try
        {
            await _apiService.AgregarVistaAutorizadaAsync(idConexion, new VistaAutorizadaRequest
            {
                NombreVista = nombreVista,
                Descripcion = descripcion
            }, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = $"Vista '{nombreVista}' autorizada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Esquema), new { id = idConexion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarTabla(int idConexion, int idTabla)
    {
        try
        {
            await _apiService.EliminarTablaAutorizadaAsync(idConexion, idTabla, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Tabla desautorizada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Esquema), new { id = idConexion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarVista(int idConexion, int idVista)
    {
        try
        {
            await _apiService.EliminarVistaAutorizadaAsync(idConexion, idVista, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Vista desautorizada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Esquema), new { id = idConexion });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activar(int id)
    {
        try
        {
            await _apiService.ActivarConexionBaseDatosAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Conexión activada.";
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
            await _apiService.DesactivarConexionBaseDatosAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Conexión desactivada.";
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
            await _apiService.EliminarConexionBaseDatosAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Conexión eliminada.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
