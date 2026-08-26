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
public class CategoriasDocumentoController : Controller
{
    private readonly IApiService _apiService;

    public CategoriasDocumentoController(IApiService apiService)
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
            var categorias = await _apiService.GetCategoriasDocumentoAsync(GetCurrentUserId(), GetIpAddress());
            return View(categorias);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar categorías: {ex.Message}";
            return View(new List<CategoriaDocumentoDto>());
        }
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View(new CrearCategoriaDocumentoRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearCategoriaDocumentoRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _apiService.CrearCategoriaDocumentoAsync(model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Categoría creada exitosamente.";
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
            var categoria = await _apiService.GetCategoriaDocumentoByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (categoria == null)
            {
                return NotFound();
            }

            var request = new ActualizarCategoriaDocumentoRequest
            {
                Nombre = categoria.Nombre,
                Descripcion = categoria.Descripcion,
                Activo = categoria.Activo
            };

            ViewBag.CategoriaId = categoria.IdCategoria;
            return View(request);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar categoría para edición: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, ActualizarCategoriaDocumentoRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.CategoriaId = id;
            return View(model);
        }

        try
        {
            await _apiService.ActualizarCategoriaDocumentoAsync(id, model, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Categoría actualizada exitosamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.CategoriaId = id;
            return View(model);
        }
    }
}
