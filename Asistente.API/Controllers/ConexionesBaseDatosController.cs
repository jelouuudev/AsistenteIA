using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConexionesBaseDatosController : ControllerBase
{
    private readonly IConexionBaseDatosService _conexionService;

    public ConexionesBaseDatosController(IConexionBaseDatosService conexionService)
    {
        _conexionService = conexionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConexionBaseDatosDto>>> GetAll()
    {
        var conexiones = await _conexionService.ObtenerTodasAsync();
        return Ok(conexiones);
    }

    [HttpGet("activas")]
    public async Task<ActionResult<IEnumerable<ConexionBaseDatosDto>>> GetActivas()
    {
        var conexiones = await _conexionService.ObtenerActivasAsync();
        return Ok(conexiones);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ConexionBaseDatosDto>> GetById(int id)
    {
        var conexion = await _conexionService.ObtenerPorIdAsync(id);
        if (conexion == null) return NotFound("Conexión no encontrada.");
        return Ok(conexion);
    }

    [HttpPost]
    public async Task<ActionResult<ConexionBaseDatosDto>> Create([FromBody] CrearConexionBaseDatosRequest request)
    {
        try
        {
            var conexion = await _conexionService.CrearAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = conexion.IdConexion }, conexion);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ConexionBaseDatosDto>> Update(int id, [FromBody] ActualizarConexionBaseDatosRequest request)
    {
        try
        {
            var conexion = await _conexionService.ActualizarAsync(id, request);
            return Ok(conexion);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _conexionService.EliminarAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activate(int id)
    {
        try
        {
            await _conexionService.ActivarAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await _conexionService.DesactivarAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("probar")]
    public async Task<ActionResult<ConexionPruebaResultadoDto>> Probar([FromBody] ProbarConexionRequest request)
    {
        var resultado = await _conexionService.ProbarConexionAsync(request);
        return Ok(resultado);
    }

    [HttpGet("{id}/esquema")]
    public async Task<ActionResult<EsquemaBaseDatosDto>> DescubrirEsquema(int id)
    {
        try
        {
            var esquema = await _conexionService.DescubrirEsquemaAsync(id);
            return Ok(esquema);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al descubrir el esquema: {ex.Message}");
        }
    }

    [HttpGet("{id}/tablas-autorizadas")]
    public async Task<ActionResult<IEnumerable<TablaAutorizadaDto>>> GetTablasAutorizadas(int id)
    {
        var tablas = await _conexionService.ObtenerTablasAutorizadasAsync(id);
        return Ok(tablas);
    }

    [HttpGet("{id}/vistas-autorizadas")]
    public async Task<ActionResult<IEnumerable<VistaAutorizadaDto>>> GetVistasAutorizadas(int id)
    {
        var vistas = await _conexionService.ObtenerVistasAutorizadasAsync(id);
        return Ok(vistas);
    }

    [HttpPost("{id}/tablas-autorizadas")]
    public async Task<IActionResult> AgregarTablaAutorizada(int id, [FromBody] TablaAutorizadaRequest request)
    {
        try
        {
            await _conexionService.AgregarTablaAutorizadaAsync(id, request);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/vistas-autorizadas")]
    public async Task<IActionResult> AgregarVistaAutorizada(int id, [FromBody] VistaAutorizadaRequest request)
    {
        try
        {
            await _conexionService.AgregarVistaAutorizadaAsync(id, request);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}/tablas-autorizadas/{idTabla}")]
    public async Task<IActionResult> EliminarTablaAutorizada(int id, int idTabla)
    {
        try
        {
            await _conexionService.EliminarTablaAutorizadaAsync(id, idTabla);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}/vistas-autorizadas/{idVista}")]
    public async Task<IActionResult> EliminarVistaAutorizada(int id, int idVista)
    {
        try
        {
            await _conexionService.EliminarVistaAutorizadaAsync(id, idVista);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
