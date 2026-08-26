using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class DebugController : Controller
{
    private readonly IApiService _apiService;

    public DebugController(IApiService apiService)
    {
        _apiService = apiService;
    }

    public async Task<IActionResult> Index(int? id)
    {
        var (userId, ip) = GetUserInfo();

        try
        {
            var conversaciones = await _apiService.GetConversacionesAsync(userId, ip);
            ViewBag.Conversaciones = conversaciones;
        }
        catch
        {
            ViewBag.Conversaciones = Enumerable.Empty<ConversacionListDto>();
        }

        if (id.HasValue)
        {
            try
            {
                var debug = await _apiService.GetDebugContextoAsync(id.Value, userId, ip);
                return View(debug);
            }
            catch
            {
                return View(null);
            }
        }

        return View(null);
    }

    [HttpPost]
    public async Task<IActionResult> Consultar(int idConversacion)
    {
        return RedirectToAction(nameof(Index), new { id = idConversacion });
    }

    private (int userId, string ip) GetUserInfo()
    {
        var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 1;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return (userId, ip);
    }
}
