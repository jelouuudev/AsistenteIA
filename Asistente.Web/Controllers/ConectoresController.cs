using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Asistente.Web.Controllers;

/// <summary>Dashboard del API Gateway Empresarial y Conectores (ETAPA 20).</summary>
[Authorize(Roles = "Administrador")]
public class ConectoresController : Controller
{
    private readonly IApiService _apiService;
    private readonly ILogger<ConectoresController> _logger;

    public ConectoresController(IApiService apiService, ILogger<ConectoresController> logger)
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

    public async Task<IActionResult> Index()
    {
        try
        {
            var conectores = (await _apiService.GetConectoresAsync(UsuarioId(), Ip())).ToList();
            return View(conectores);
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar conectores: {ex.Message}";
            return View(new System.Collections.Generic.List<ConectorDto>());
        }
    }

    public async Task<IActionResult> Detalle(string id)
    {
        var conector = await _apiService.GetConectorAsync(id, UsuarioId(), Ip());
        if (conector == null)
        {
            TempData["ErrorMessage"] = $"No existe el conector '{id}'.";
            return RedirectToAction(nameof(Index));
        }
        ConnectorMetricas? metricas = null;
        System.Collections.Generic.IEnumerable<ConectorEjecucionDto> auditoria =
            new System.Collections.Generic.List<ConectorEjecucionDto>();
        try
        {
            metricas = await _apiService.GetConectorMetricasAsync(conector.IdConnector, UsuarioId(), Ip());
            auditoria = await _apiService.GetConectorAuditoriaAsync(conector.IdConnector, UsuarioId(), Ip());
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar métricas: {ex.Message}";
        }
        ViewBag.Metricas = metricas;
        ViewBag.Auditoria = auditoria;
        return View(conector);
    }

    [HttpGet]
    public IActionResult Crear() => View(new CrearConectorRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearConectorRequest request,
        string? baseUrl, string? recursoDefecto, string? metodoDefecto, string? tipoCredencial,
        string? nombreUsuario, string? secreto)
    {
        try
        {
            request.Tipo = string.IsNullOrWhiteSpace(request.Tipo) ? "REST" : request.Tipo;
            // La URL va a la clave que cada tipo lee (el formulario usa un solo campo).
            var claveUrl = request.Tipo switch
            {
                "SOAP" => "Endpoint",
                "SharePoint" => "SiteUrl",
                "Webhook" => "Url",
                "SMTP" => "Host",
                _ => "BaseUrl"
            };
            if (!string.IsNullOrWhiteSpace(baseUrl))
                request.Configuracion[claveUrl] = baseUrl.Trim();
            if (!string.IsNullOrWhiteSpace(recursoDefecto))
                request.Configuracion["RecursoDefecto"] = recursoDefecto.Trim();
            if (!string.IsNullOrWhiteSpace(metodoDefecto))
                request.Configuracion["MetodoDefecto"] = metodoDefecto.Trim();
            if (!string.IsNullOrWhiteSpace(tipoCredencial) && tipoCredencial != "None")
                request.Credencial = new CrearCredencialRequest
                {
                    Tipo = tipoCredencial,
                    NombreUsuario = nombreUsuario,
                    Secreto = secreto
                };
            var creado = await _apiService.RegistrarConectorAsync(request, UsuarioId(), Ip());
            TempData["OkMessage"] = $"Conector '{creado.Codigo}' registrado.";
            return RedirectToAction(nameof(Detalle), new { id = creado.Codigo });
        }
        catch (System.Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al registrar: {ex.Message}";
            return View(request);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Probar(string id)
    {
        try
        {
            var (ok, error) = await _apiService.ProbarConectorAsync(id, UsuarioId(), Ip());
            TempData[ok ? "OkMessage" : "ErrorMessage"] =
                ok ? "Conexión exitosa." : $"Falló la prueba: {error}";
        }
        catch (System.Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(string id, bool activo)
    {
        try
        {
            await _apiService.CambiarEstadoConectorAsync(id, activo, UsuarioId(), Ip());
            TempData["OkMessage"] = activo ? "Conector habilitado." : "Conector deshabilitado.";
        }
        catch (System.Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ejecutar(string id, string? operacion, string? recurso,
        string? metodo, string? cuerpo, string? parametros)
    {
        try
        {
            var request = new EjecutarConectorRequest
            {
                Operacion = operacion,
                Recurso = recurso,
                Metodo = string.IsNullOrWhiteSpace(metodo) ? "GET" : metodo,
                Cuerpo = cuerpo,
                Parametros = new System.Collections.Generic.Dictionary<string, string>()
            };
            // Líneas "clave=valor" (una por línea) para parámetros del conector
            // (p. ej. Asunto del correo, Adjuntos con rutas del servidor).
            if (!string.IsNullOrWhiteSpace(parametros))
            {
                foreach (var linea in parametros.Split('\n'))
                {
                    var i = linea.IndexOf('=');
                    if (i > 0)
                        request.Parametros[linea[..i].Trim()] = linea[(i + 1)..].Trim();
                }
            }
            var resultado = await _apiService.EjecutarConectorAsync(id, request, UsuarioId(), Ip());
            var exitoso = resultado.TryGetProperty("exitoso", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.True;
            string? error = resultado.TryGetProperty("error", out var pErr) && pErr.ValueKind == System.Text.Json.JsonValueKind.String
                ? pErr.GetString() : null;
            TempData[exitoso ? "OkMessage" : "ErrorMessage"] = exitoso
                ? "Ejecución exitosa: revisa la auditoría abajo."
                : $"Falló la ejecución: {error}";
        }
        catch (System.Exception ex) { TempData["ErrorMessage"] = $"Error: {ex.Message}"; }
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
