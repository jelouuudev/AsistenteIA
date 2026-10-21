using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

// Sin rol fijo aquí: cada acción declara el suyo ([Authorize] se suma en AND,
// no se puede "abrir" una acción hija si el controlador ya exige Administrador).
[Authorize]
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

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Dashboard()
    {
        var dto = await _apiService.GetDashboardSeguridadAsync(GetCurrentUserId(), GetIpAddress());
        return View(dto);
    }

    // Códigos mostrables por rol en la matriz. Lo no listado ni se muestra
    // (nunca se marcará). Administrador ve todo.
    private static readonly Dictionary<string, string[]> CodigosVisiblesPorRol = new()
    {
        ["Operador"] = new[] { "HERRAMIENTAS_CONSULTAR", "SQL_CONSULTAR" },
        ["Usuario"] = new[] { "HERRAMIENTAS_CONSULTAR", "SQL_CONSULTAR" },
        ["Supervisor"] = new[] { "HERRAMIENTAS_CONSULTAR", "SQL_CONSULTAR", "AUDITORIA_CONSULTAR" }
    };

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Permisos(int? idRol)
    {
        var permisos = await _apiService.GetPermisosAsync(GetCurrentUserId(), GetIpAddress());
        var roles = (await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress())).ToList();
        ViewBag.Roles = roles;
        ViewBag.IdRol = idRol;
        var matriz = new List<PermisoAsignadoDto>();
        if (idRol.HasValue)
        {
            matriz = await _apiService.GetPermisosDeRolAsync(idRol.Value, GetCurrentUserId(), GetIpAddress());
            var nombreRol = roles.FirstOrDefault(r => r.IdRol == idRol.Value)?.Nombre ?? "";
            if (CodigosVisiblesPorRol.TryGetValue(nombreRol, out var visibles))
                matriz = matriz.Where(m => visibles.Contains(m.Codigo)).ToList();
        }
        ViewBag.Matriz = matriz;
        return View(permisos);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Politicas()
    {
        var politicas = await _apiService.GetPoliticasAsync(GetCurrentUserId(), GetIpAddress());
        return View(politicas);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Usuarios()
    {
        var usuarios = await _apiService.GetUsuariosAsync(GetCurrentUserId(), GetIpAddress());
        return View(usuarios);
    }

    /// <summary>
    /// Con y sin id en la ruta. La ruta por defecto del Web es
    /// {controller}/{action}/{id?}: como el parametro se llama idUsuario (no id),
    /// /Seguridad/AsignarAsistentes/7 llegaba con idUsuario = 0 y la pantalla
    /// ensenaba la del usuario 0 (vacia). Con la ruta explicita el id se
    /// bindea bien; la variante sin id sigue sirviendo el enlace antiguo por
    /// query string.
    /// </summary>
    [HttpGet("~/Seguridad/AsignarAsistentes")]
    [HttpGet("~/Seguridad/AsignarAsistentes/{idUsuario:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AsignarAsistentes(int idUsuario)
    {
        var asistentes = await _apiService.GetAsistentesDeUsuarioAsync(idUsuario, GetCurrentUserId(), GetIpAddress());
        ViewBag.IdUsuario = idUsuario;
        return View(asistentes);
    }

    [HttpPost("~/Seguridad/AsignarAsistentes")]
    [HttpPost("~/Seguridad/AsignarAsistentes/{idUsuario:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AsignarAsistentes(int idUsuario, List<AsistenteAutorizadoDto> items)
    {
        // Si el binding no llega (lista null), NO se llama a la API: la API
        // "borra y reasigna", y con una lista vacia eso se traducía en borrar
        // todas las asignaciones del usuario sin querer. Desasignar de verdad se
        // hace desmarcando los checks (items llega con todos en false).
        if (items == null)
        {
            TempData["ErrorMessage"] = "No se recibió la lista de asignaciones: no se ha modificado nada.";
            return RedirectToAction("AsignarAsistentes", new { idUsuario });
        }

        try
        {
            var ids = items
                .Where(x => x.Autorizado)
                .Select(x => x.IdAsistente)
                .ToList();
            await _apiService.AsignarAsistentesUsuarioAsync(idUsuario,
                new AsignarAsistentesUsuarioRequest { IdUsuario = idUsuario, IdsAsistentes = ids },
                GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Asistentes asignados.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Acceso denegado: {ex.Message}";
        }
        return RedirectToAction("AsignarAsistentes", new { idUsuario });
    }

    public IActionResult AsignarPermisos(int idRol)
        => RedirectToAction("Permisos", new { idRol });

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AsignarPermisos(int idRol, List<PermisoAsignadoDto> items)
    {
        try
        {
            var codigos = (items ?? new List<PermisoAsignadoDto>())
                .Where(x => x.Asignado)
                .Select(x => x.Codigo)
                .ToList();
            // Conservar asignados ocultos por el filtro de visibilidad (no se muestran,
            // no se tocan): leer matriz completa y fusionar.
            var completa = await _apiService.GetPermisosDeRolAsync(idRol, GetCurrentUserId(), GetIpAddress());
            foreach (var p in completa.Where(p => p.Asignado && !codigos.Contains(p.Codigo)))
            {
                var enVista = (items ?? new List<PermisoAsignadoDto>()).Any(x => x.Codigo == p.Codigo);
                if (!enVista) codigos.Add(p.Codigo);
            }
            await _apiService.AsignarPermisosRolAsync(idRol,
                new AsignarPermisosRolRequest { IdRol = idRol, Codigos = codigos },
                GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Permisos del rol actualizados.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Acceso denegado: {ex.Message}";
        }
        return RedirectToAction("Permisos", new { idRol });
    }

    [HttpGet("~/Seguridad/AsignarFuentes")]
    [HttpGet("~/Seguridad/AsignarFuentes/{idUsuario:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AsignarFuentes(int idUsuario)
    {
        var fuentes = await _apiService.GetFuentesDeUsuarioAsync(idUsuario, GetCurrentUserId(), GetIpAddress());
        ViewBag.IdUsuario = idUsuario;
        return View(fuentes);
    }

    [HttpPost("~/Seguridad/AsignarFuentes")]
    [HttpPost("~/Seguridad/AsignarFuentes/{idUsuario:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AsignarFuentes(int idUsuario, List<FuenteAutorizadaDto> items)
    {
        // Misma proteccion que en AsignarAsistentes: sin lista recibida no se
        // llama a la API (que borra y reasigna).
        if (items == null)
        {
            TempData["ErrorMessage"] = "No se recibió la lista de fuentes: no se ha modificado nada.";
            return RedirectToAction("AsignarFuentes", new { idUsuario });
        }

        try
        {
            var ids = items
                .Where(x => x.Autorizada)
                .Select(x => x.IdFuente)
                .ToList();
            await _apiService.AsignarFuentesUsuarioAsync(idUsuario,
                new AsignarFuentesUsuarioRequest { IdUsuario = idUsuario, IdsFuentes = ids },
                GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Fuentes asignadas.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Acceso denegado: {ex.Message}";
        }
        return RedirectToAction("AsignarFuentes", new { idUsuario });
    }
}
