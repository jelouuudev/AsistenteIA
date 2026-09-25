using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Aprobaciones;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities.Aprobaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/aprobaciones")]
[Authorize]
public class AprobacionesController : ControllerBase
{
    private readonly IApprovalRequestRepository _reqRepo;
    private readonly IApprovalPolicyRepository _polRepo;
    private readonly ApprovalManager _manager;

    public AprobacionesController(
        IApprovalRequestRepository reqRepo,
        IApprovalPolicyRepository polRepo,
        ApprovalManager manager)
    {
        _reqRepo = reqRepo;
        _polRepo = polRepo;
        _manager = manager;
    }

    private int UsuarioId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>Dashboard del Centro de Aprobaciones (Actividad 11): conteos por estado.</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult> Dashboard(CancellationToken ct)
    {
        var todas = await _reqRepo.GetAllAsync(ct);
        var dto = new
        {
            Total = todas.Count,
            Pendientes = todas.Count(a => a.Estado == EstadoAprobacion.Pendiente),
            EnRevision = todas.Count(a => a.Estado == EstadoAprobacion.EnRevision),
            Aprobadas = todas.Count(a => a.Estado == EstadoAprobacion.Aprobado),
            Rechazadas = todas.Count(a => a.Estado == EstadoAprobacion.Rechazado),
            Expiradas = todas.Count(a => a.Estado == EstadoAprobacion.Expirado),
            Delegadas = todas.Count(a => a.Estado == EstadoAprobacion.Delegado),
            Canceladas = todas.Count(a => a.Estado == EstadoAprobacion.Cancelado),
            Solicitudes = todas.Select(ToDto).ToList()
        };
        return Ok(dto);
    }

    /// <summary>Bandeja personal del usuario autenticado (Actividad 12): sus aprobaciones pendientes.</summary>
    [HttpGet("bandeja")]
    public async Task<ActionResult> Bandeja(CancellationToken ct)
    {
        var pendientes = await _reqRepo.GetPendientesParaAsync(UsuarioId(), ct);
        return Ok(pendientes.Select(ToDto).ToList());
    }

    /// <summary>Historial filtrable (Actividad 13).</summary>
    [HttpGet("historial")]
    public async Task<ActionResult> Historial(CancellationToken ct)
    {
        var todas = await _reqRepo.GetAllAsync(ct);
        return Ok(todas.Select(ToDto).ToList());
    }

    /// <summary>Crea una solicitud de aprobación (Actividad 1).</summary>
    [HttpPost]
    public async Task<ActionResult> CrearSolicitud([FromBody] CrearSolicitudRequest req, CancellationToken ct)
    {
        try
        {
            var resultado = await _manager.CrearSolicitudAsync(
                req.IdPlan, req.Tipo, UsuarioId(), req.Observaciones, req.Aprobadores, req.IdPolicy, ct);
            return Ok(ToDto(resultado));
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Decide (aprobar / rechazar) una solicitud (Actividades 9 y 10).</summary>
    [HttpPost("{id:int}/decidir")]
    public async Task<ActionResult> Decidir(int id, [FromBody] DecidirRequest req, CancellationToken ct)
    {
        if (req.Decision != "Aprobar" && req.Decision != "Rechazar")
            return BadRequest("La decisión debe ser 'Aprobar' o 'Rechazar'.");
        try
        {
            var resultado = await _manager.DecidirAsync(id, UsuarioId(), req.Decision, req.Comentario, ct);
            return Ok(ToDto(resultado));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { exitoso = false, error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Delega una aprobación a otro usuario (Actividad 7).</summary>
    [HttpPost("{id:int}/delegar")]
    public async Task<ActionResult> Delegar(int id, [FromBody] DelegarRequest req, CancellationToken ct)
    {
        if (req.IdUsuarioDestino <= 0)
            return BadRequest("Debe indicar un ID de usuario destino para delegar la solicitud.");
        try
        {
            var resultado = await _manager.DelegarAsync(id, UsuarioId(), req.IdUsuarioDestino, req.Comentario, ct);
            return Ok(ToDto(resultado));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { exitoso = false, error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Marca como expirada (Actividad 8) — usado por un scheduler o manualmente.</summary>
    [HttpPost("{id:int}/expirar")]
    public async Task<ActionResult> Expirar(int id, CancellationToken ct)
    {
        var resultado = await _manager.ExpirarAsync(id, ct);
        return Ok(ToDto(resultado));
    }

    /// <summary>Políticas configurables (Actividad 2).</summary>
    [HttpGet("politicas")]
    public async Task<ActionResult> Politicas(CancellationToken ct)
        => Ok((await _polRepo.GetAllAsync(ct)).Select(p => new
        {
            p.IdPolicy, p.Nombre, p.CantidadMinimaAprobaciones,
            p.RequiereUnanimidad, p.PermiteDelegacion, p.TiempoMaximoHoras, p.Activo
        }));

    [HttpPost("politicas")]
    public async Task<ActionResult> CrearPolitica([FromBody] ApprovalPolicy politica, CancellationToken ct)
    {
        var creada = await _polRepo.AddAsync(politica, ct);
        return Ok(new { creada.IdPolicy });
    }

    private static object ToDto(ApprovalRequest a) => new
    {
        a.IdApproval, a.Codigo, a.IdPlan, Tipo = a.Tipo.ToString(), Estado = a.Estado.ToString(),
        a.FechaSolicitud, a.FechaVencimiento, a.Solicitante, a.Observaciones,
        Asignados = a.Asignados.Select(x => new { x.IdAssignee, x.IdUsuario, x.EsPrincipal, x.Estado }).ToList(),
        Decisiones = a.Decisiones.Select(d => new { d.IdDecision, d.IdUsuario, d.Decision, d.Comentario, d.FechaDecision }).ToList()
    };
}

public class DecidirRequest { public string Decision { get; set; } = "Aprobar"; public string? Comentario { get; set; } }
public class DelegarRequest { public int IdUsuarioDestino { get; set; } public string? Comentario { get; set; } }
public class CrearSolicitudRequest { public int IdPlan { get; set; } public TipoAprobacion Tipo { get; set; } public string Observaciones { get; set; } = string.Empty; public List<int> Aprobadores { get; set; } = new(); public int? IdPolicy { get; set; } }
