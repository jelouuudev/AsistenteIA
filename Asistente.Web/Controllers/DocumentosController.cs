using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Asistente.Web.Controllers;

[Authorize(Roles = "Administrador,Operador,Supervisor,Usuario")]
public class DocumentosController : Controller
{
    private readonly IApiService _apiService;

    public DocumentosController(IApiService apiService)
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

    public async Task<IActionResult> Index(FiltroDocumentoRequest filtro)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ip = GetIpAddress();

            var documentos = await _apiService.GetDocumentosFiltradosAsync(filtro, userId, ip);
            var categorias = await _apiService.GetCategoriasDocumentoActivasAsync(userId, ip);

            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre", filtro.IdCategoria);
            ViewBag.Estados = new SelectList(new[] { "Activo", "Archivado" }, filtro.Estado);
            ViewBag.Filtro = filtro;

            return View(documentos);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar documentos: {ex.Message}";
            return View(new List<DocumentoDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        try
        {
            var categorias = await _apiService.GetCategoriasDocumentoActivasAsync(GetCurrentUserId(), GetIpAddress());
            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre");
            return View(new CrearDocumentoConArchivoModel());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar formulario de creación: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearDocumentoConArchivoModel model)
    {
        var userId = GetCurrentUserId();
        var ip = GetIpAddress();

        if (model.Archivo == null || model.Archivo.Length == 0)
        {
            ModelState.AddModelError("Archivo", "El archivo PDF es obligatorio para registrar el documento.");
        }

        if (!ModelState.IsValid)
        {
            var categorias = await _apiService.GetCategoriasDocumentoActivasAsync(userId, ip);
            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre", model.IdCategoria);
            return View(model);
        }

        try
        {
            // 1. Crear el documento (metadatos)
            var request = new CrearDocumentoRequest
            {
                Codigo = model.Codigo,
                Nombre = model.Nombre,
                Descripcion = model.Descripcion,
                IdCategoria = model.IdCategoria
            };

            var docCreated = await _apiService.CrearDocumentoAsync(request, userId, ip);

            // 2. Cargar el archivo PDF para la primera versión
            using (var stream = model.Archivo!.OpenReadStream())
            {
                await _apiService.CargarDocumentoVersionAsync(docCreated.IdDocumento, model.Archivo.FileName, stream, userId, ip);
            }

            // 3. Activar el documento automáticamente si se cargó la v1 con éxito
            await _apiService.ActivarDocumentoAsync(docCreated.IdDocumento, userId, ip);

            TempData["SuccessMessage"] = "Documento registrado y activado con éxito.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var categorias = await _apiService.GetCategoriasDocumentoActivasAsync(userId, ip);
            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre", model.IdCategoria);
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Operador,Supervisor")]
    public async Task<IActionResult> Editar(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ip = GetIpAddress();

            var doc = await _apiService.GetDocumentoByIdAsync(id, userId, ip);
            if (doc == null)
            {
                return NotFound();
            }

            var categorias = await _apiService.GetCategoriasDocumentoActivasAsync(userId, ip);
            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre", doc.IdCategoria);
            ViewBag.DocumentoId = doc.IdDocumento;

            var request = new ActualizarDocumentoRequest
            {
                Nombre = doc.Nombre,
                Descripcion = doc.Descripcion,
                IdCategoria = doc.IdCategoria
            };

            return View(request);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar documento para edición: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Operador,Supervisor")]
    public async Task<IActionResult> Editar(int id, ActualizarDocumentoRequest model)
    {
        var userId = GetCurrentUserId();
        var ip = GetIpAddress();

        if (!ModelState.IsValid)
        {
            var categorias = await _apiService.GetCategoriasDocumentoActivasAsync(userId, ip);
            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre", model.IdCategoria);
            ViewBag.DocumentoId = id;
            return View(model);
        }

        try
        {
            await _apiService.ActualizarDocumentoAsync(id, model, userId, ip);
            TempData["SuccessMessage"] = "Metadatos del documento actualizados exitosamente.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var categorias = await _apiService.GetCategoriasDocumentoActivasAsync(userId, ip);
            ViewBag.Categorias = new SelectList(categorias, "IdCategoria", "Nombre", model.IdCategoria);
            ViewBag.DocumentoId = id;
            return View(model);
        }
    }

    public async Task<IActionResult> Detalle(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ip = GetIpAddress();

            var doc = await _apiService.GetDocumentoByIdAsync(id, userId, ip);
            if (doc == null)
            {
                return NotFound();
            }

            var versiones = await _apiService.GetDocumentoVersionesAsync(id, userId, ip);
            var auditorias = await _apiService.GetAuditoriaDocumentoAsync(id, userId, ip);

            ViewBag.Versiones = versiones;
            ViewBag.Auditorias = auditorias;

            return View(doc);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar detalle del documento: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> CargarVersion(int id)
    {
        try
        {
            var doc = await _apiService.GetDocumentoByIdAsync(id, GetCurrentUserId(), GetIpAddress());
            if (doc == null)
            {
                return NotFound();
            }
            return View(doc);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CargarVersion(int id, IFormFile archivo)
    {
        var userId = GetCurrentUserId();
        var ip = GetIpAddress();

        if (archivo == null || archivo.Length == 0)
        {
            TempData["ErrorMessage"] = "Debe seleccionar un archivo PDF válido.";
            return RedirectToAction(nameof(CargarVersion), new { id });
        }

        try
        {
            using (var stream = archivo.OpenReadStream())
            {
                await _apiService.CargarDocumentoVersionAsync(id, archivo.FileName, stream, userId, ip);
            }

            TempData["SuccessMessage"] = "Nueva versión cargada correctamente.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar versión: {ex.Message}";
            return RedirectToAction(nameof(CargarVersion), new { id });
        }
    }

    public async Task<IActionResult> Descargar(int id, int versionId)
    {
        try
        {
            var (stream, fileName, contentType) = await _apiService.DescargarDocumentoVersionAsync(id, versionId, GetCurrentUserId(), GetIpAddress());
            // Nota: FileStreamResult lee el stream directamente.
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al descargar archivo: {ex.Message}";
            return RedirectToAction(nameof(Detalle), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Operador,Supervisor")]
    public async Task<IActionResult> Activar(int id)
    {
        try
        {
            await _apiService.ActivarDocumentoAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento activado correctamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al activar documento: {ex.Message}";
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Operador,Supervisor")]
    public async Task<IActionResult> Archivar(int id)
    {
        try
        {
            await _apiService.ArchivarDocumentoAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento archivado correctamente.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al archivar documento: {ex.Message}";
        }
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Operador,Supervisor")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await _apiService.EliminarDocumentoAsync(id, GetCurrentUserId(), GetIpAddress());
            TempData["SuccessMessage"] = "Documento eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al eliminar documento: {ex.Message}";
            return RedirectToAction(nameof(Detalle), new { id });
        }
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> AuditoriaCompleta()
    {
        try
        {
            var auditorias = await _apiService.GetTodasAuditoriasDocumentoAsync(GetCurrentUserId(), GetIpAddress());
            return View(auditorias);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al eliminar documento: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }
}

public class CrearDocumentoConArchivoModel
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdCategoria { get; set; }
    public IFormFile? Archivo { get; set; }
}
