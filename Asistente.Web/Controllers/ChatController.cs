using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Operador,Supervisor")]
public class ChatController : Controller
{
    private readonly IApiService _apiService;

    public ChatController(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<IActionResult> Index()
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            var conversaciones = await _apiService.GetConversacionesAsync(userId, ip);
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
            ViewBag.ConversacionesJson = System.Text.Json.JsonSerializer.Serialize(conversaciones.Select(c => new {
                c.IdConversacion,
                c.Titulo,
                c.Estado,
                c.TotalMensajes,
                c.IdAsistente,
                Fecha = c.FechaUltimaActividad?.ToString("dd/MM HH:mm") ?? c.FechaInicio.ToString("dd/MM HH:mm")
            }), jsonOptions);
        }
        catch
        {
            ViewBag.ConversacionesJson = "[]";
        }
        return View();
    }

    [HttpPost]
    public async Task<ActionResult<MensajeResponse>> Enviar([FromBody] MensajeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mensaje))
        {
            return BadRequest(new MensajeResponse
            {
                Exitoso = false,
                Error = "El mensaje no puede estar vacío."
            });
        }

        var (userId, ip) = GetUserInfo();
        request.UsuarioPropietario = userId;

        var response = await _apiService.EnviarMensajeAsync(request);
        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConversacionListDto>>> Listar()
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            var conversaciones = await _apiService.GetConversacionesAsync(userId, ip);
            return Ok(conversaciones);
        }
        catch
        {
            return Ok(Array.Empty<ConversacionListDto>());
        }
    }

    [HttpGet("Chat/Buscar")]
    public async Task<ActionResult<IEnumerable<ConversacionListDto>>> Buscar([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<ConversacionListDto>());

        var (userId, ip) = GetUserInfo();
        try
        {
            var conversaciones = await _apiService.BuscarConversacionesAsync(q, userId, ip);
            return Ok(conversaciones);
        }
        catch
        {
            return Ok(Array.Empty<ConversacionListDto>());
        }
    }

    [HttpGet("Chat/Obtener/{id}")]
    public async Task<ActionResult<ConversacionDto>> Obtener(int id)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            var conversacion = await _apiService.GetConversacionByIdAsync(id, userId, ip);
            if (conversacion == null)
                return NotFound();
            return Ok(conversacion);
        }
        catch
        {
            return NotFound();
        }
    }

    [HttpPost("Chat/Nueva")]
    public async Task<ActionResult<ConversacionDto>> Nueva()
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            var conversacion = await _apiService.CrearConversacionAsync(userId, ip);
            return Ok(conversacion);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("Chat/Renombrar")]
    public async Task<IActionResult> Renombrar(int id, string titulo)
    {
        var (userId, ip) = GetUserInfo();
        try
        {
            await _apiService.RenombrarConversacionAsync(id, titulo, userId, ip);
            return Ok(new { exitoso = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private (int userId, string ip) GetUserInfo()
    {
        var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 1;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return (userId, ip);
    }
}
