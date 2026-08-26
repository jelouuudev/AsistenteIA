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
public class ConsultasEjecutadasController : ControllerBase
{
    private readonly IConsultaEjecutadaService _consultaService;

    public ConsultasEjecutadasController(IConsultaEjecutadaService consultaService)
    {
        _consultaService = consultaService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConsultaEjecutadaDto>>> GetAll()
    {
        var consultas = await _consultaService.ObtenerTodasAsync();
        return Ok(consultas);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ConsultaEjecutadaDto>> GetById(int id)
    {
        var consulta = await _consultaService.ObtenerPorIdAsync(id);
        if (consulta == null) return NotFound("Consulta no encontrada.");
        return Ok(consulta);
    }

    [HttpGet("usuario/{idUsuario}")]
    public async Task<ActionResult<IEnumerable<ConsultaEjecutadaDto>>> GetByUsuario(int idUsuario)
    {
        var consultas = await _consultaService.ObtenerPorUsuarioAsync(idUsuario);
        return Ok(consultas);
    }

    [HttpGet("conexion/{idConexion}")]
    public async Task<ActionResult<IEnumerable<ConsultaEjecutadaDto>>> GetByConexion(int idConexion)
    {
        var consultas = await _consultaService.ObtenerPorConexionAsync(idConexion);
        return Ok(consultas);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardConsultasDto>> GetDashboard()
    {
        var dashboard = await _consultaService.ObtenerDashboardAsync();
        return Ok(dashboard);
    }
}
