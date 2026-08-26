using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class SeguridadController : Controller
{
    private readonly IApiService _apiService;

    public SeguridadController(IApiService apiService)
    {
        _apiService = apiService;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(idClaim, out var id) ? id : 1;
    }

    private string GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Dashboard()
    {
        var dto = await _apiService.GetDashboardSeguridadAsync(GetCurrentUserId(), GetIpAddress());
        return View(dto);
    }

    public async Task<IActionResult> Permisos()
    {
        var permisos = await _apiService.GetPermisosAsync(GetCurrentUserId(), GetIpAddress());
        return View(permisos);
    }

    public async Task<IActionResult> Politicas()
    {
        var politicas = await _apiService.GetPoliticasAsync(GetCurrentUserId(), GetIpAddress());
        return View(politicas);
    }

    public async Task<IActionResult> Usuarios()
    {
        var usuarios = await _apiService.GetUsuariosAsync(GetCurrentUserId(), GetIpAddress());
        return View(usuarios);
    }

    public async Task<IActionResult> AsignarAsistentes(int idUsuario)
    {
        var asistentes = await _apiService.GetAsistentesDeUsuarioAsync(idUsuario, GetCurrentUserId(), GetIpAddress());
        ViewBag.IdUsuario = idUsuario;
        return View(asistentes);
    }

    [HttpPost]
    public async Task<IActionResult> AsignarAsistentes(int idUsuario, List<AsistenteAutorizadoDto> items)
    {
        var ids = (items ?? new List<AsistenteAutorizadoDto>())
            .Where(x => x.Autorizado)
            .Select(x => x.IdAsistente)
            .ToList();
        await _apiService.AsignarAsistentesUsuarioAsync(idUsuario,
            new AsignarAsistentesUsuarioRequest { IdUsuario = idUsuario, IdsAsistentes = ids },
            GetCurrentUserId(), GetIpAddress());
        return RedirectToAction("AsignarAsistentes", new { idUsuario });
    }

    public async Task<IActionResult> AsignarFuentes(int idUsuario)
    {
        var fuentes = await _apiService.GetFuentesDeUsuarioAsync(idUsuario, GetCurrentUserId(), GetIpAddress());
        ViewBag.IdUsuario = idUsuario;
        return View(fuentes);
    }

    [HttpPost]
    public async Task<IActionResult> AsignarFuentes(int idUsuario, List<FuenteAutorizadaDto> items)
    {
        var ids = (items ?? new List<FuenteAutorizadaDto>())
            .Where(x => x.Autorizada)
            .Select(x => x.IdFuente)
            .ToList();
        await _apiService.AsignarFuentesUsuarioAsync(idUsuario,
            new AsignarFuentesUsuarioRequest { IdUsuario = idUsuario, IdsFuentes = ids },
            GetCurrentUserId(), GetIpAddress());
        return RedirectToAction("AsignarFuentes", new { idUsuario });
    }
}
