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
public class RolesController : Controller
{
    private readonly IApiService _apiService;

    public RolesController(IApiService apiService)
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
            var roles = await _apiService.GetRolesAsync(GetCurrentUserId(), GetIpAddress());
            return View(roles);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar roles: {ex.Message}";
            return View(new List<RolDto>());
        }
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View(new CrearRolRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearRolRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _apiService.CrearRolAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Rol creado exitosamente.";
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
            var rol = await _apiService.GetRolByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (rol == null)
            {
                return NotFound();
            }

            var request = new ActualizarRolRequest
            {
                Nombre = rol.Nombre,
                Descripcion = rol.Descripcion,
                Activo = rol.Activo
            };

            ViewBag.RolId = rol.IdRol;
            return View(request);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar rol para edición: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarRolRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _apiService.ActualizarRolAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Rol actualizado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
