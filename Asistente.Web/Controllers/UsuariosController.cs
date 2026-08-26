using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class UsuariosController : Controller
{
    private readonly IApiService _apiService;

    public UsuariosController(IApiService apiService)
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
            var usuarios = await _apiService.GetUsuariosAsync(GetCurrentUserId(), GetIpAddress());
            return View(usuarios);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar usuarios: {ex.Message}";
            return View(new List<UsuarioDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        try
        {
            var roles = await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Roles = roles.Where(r => r.Activo).ToList();
            return View(new CrearUsuarioRequest());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar roles para la creación: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearUsuarioRequest model, List<string> selectedRoles)
    {
        model.Roles = selectedRoles ?? new List<string>();

        if (!ModelState.IsValid)
        {
            var roles = await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Roles = roles.Where(r => r.Activo).ToList();
            return View(model);
        }

        try
        {
            await _apiService.CrearUsuarioAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Usuario creado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var roles = await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Roles = roles.Where(r => r.Activo).ToList();
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var user = await _apiService.GetUsuarioByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Roles = roles.Where(r => r.Activo).ToList();

            var request = new ActualizarUsuarioRequest
            {
                Nombres = user.Nombres,
                Apellidos = user.Apellidos,
                Correo = user.Correo,
                Activo = user.Activo,
                Roles = user.Roles
            };

            ViewBag.Username = user.UsuarioNombre;
            ViewBag.UserId = user.IdUsuario;

            return View(request);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar usuario para edición: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarUsuarioRequest model, List<string> selectedRoles)
    {
        model.Roles = selectedRoles ?? new List<string>();

        if (!ModelState.IsValid)
        {
            var roles = await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Roles = roles.Where(r => r.Activo).ToList();
            return View(model);
        }

        try
        {
            await _apiService.ActualizarUsuarioAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Usuario actualizado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var roles = await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Roles = roles.Where(r => r.Activo).ToList();
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> CambiarPassword(int id)
    {
        try
        {
            var user = await _apiService.GetUsuarioByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (user == null) return NotFound();

            ViewBag.Username = user.UsuarioNombre;
            ViewBag.UserId = user.IdUsuario;

            return View(new CambiarPasswordRequest());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar datos del usuario: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarPassword(int id, CambiarPasswordRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _apiService.CambiarPasswordAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Contraseña cambiada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(int id)
    {
        try
        {
            await _apiService.DesactivarUsuarioAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Usuario desactivado exitosamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al desactivar usuario: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
