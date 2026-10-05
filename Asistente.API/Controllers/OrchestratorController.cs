using System;
using System.Collections.Generic;
using System.Security.Claims;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/orchestrator")]
[Authorize(Roles = "Administrador,Operador,Supervisor")]
public class OrchestratorController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IAgentExecutionRepository _execRepo;
    private readonly IAgentExecutionTraceRepository _traceRepo;
    private readonly IAgentCollaborationRuleRepository _reglasRepo;
    private readonly IAsistenteRepository _asistenteRepo;
    private readonly IServiceScopeFactory _scopeFactory;

    public OrchestratorController(
        IAgentOrchestrator orchestrator,
        IAgentExecutionRepository execRepo,
        IAgentExecutionTraceRepository traceRepo,
        IAgentCollaborationRuleRepository reglasRepo,
        IAsistenteRepository asistenteRepo,
        IServiceScopeFactory scopeFactory)
    {
        _orchestrator = orchestrator;
        _execRepo = execRepo;
        _traceRepo = traceRepo;
        _reglasRepo = reglasRepo;
        _asistenteRepo = asistenteRepo;
        _scopeFactory = scopeFactory;
    }

    private int UsuarioId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>Ejecuta una solicitud a través del Orchestrator (colaboración multi-agente).</summary>
    [HttpPost("execute")]
    public async Task<ActionResult<AgentExecutionResult>> Execute(
        [FromBody] AgentRequest request, CancellationToken cancellationToken)
    {
        if (request.IdAgentePrincipal <= 0 || string.IsNullOrWhiteSpace(request.Pregunta))
            return BadRequest("Debe indicar el agente principal y la pregunta.");

        request.IdUsuario = UsuarioId();
        request.PermitirColaboracion = true;

        // Crea la ejecución y devuelve el IdExecution de inmediato (la orquestación puede tardar
        // minutos en CPU). El grafo se ejecuta en segundo plano en un scope propio para no
        // depender del ciclo de vida del DbContext de la petición HTTP.
        var idExecution = await _orchestrator.IniciarAsync(request, cancellationToken);
        var requestBg = request;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orch = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
                await orch.EjecutarGrafoAsync(idExecution, requestBg, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR background orchestrator (Execution {idExecution}): {ex}");
            }
        });

        return Ok(new AgentExecutionResult { IdExecution = idExecution });
    }

    /// <summary>Dashboard: ejecuciones recientes con métricas de colaboración.</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<object>> Dashboard(CancellationToken cancellationToken)
    {
        var ejecuciones = await _execRepo.GetRecentAsync(50, cancellationToken);
        var resumen = new
        {
            TotalEjecuciones = ejecuciones.Count,
            Completadas = ejecuciones.Count(e => e.Estado == "Completado"),
            ConError = ejecuciones.Count(e => e.Estado == "Error"),
            TiempoPromedioMs = ejecuciones.Where(e => e.TiempoTotalMs.HasValue)
                .Select(e => e.TiempoTotalMs!.Value).DefaultIfEmpty(0).Average(),
            Ejecuciones = ejecuciones.Select(e => new
            {
                e.IdExecution,
                AgentePrincipal = e.AgentePrincipal?.Nombre,
                e.Pregunta,
                e.Estado,
                e.CantidadAgentes,
                e.ProfundidadAlcanzada,
                e.HerramientasUtilizadas,
                e.TiempoTotalMs,
                e.FechaInicio
            })
        };
        return Ok(resumen);
    }

    /// <summary>Visualizador de trazas de una ejecución (Actividad 13).</summary>
    [HttpGet("trazas/{idExecution}")]
    public async Task<ActionResult<object>> Trazas(int idExecution, CancellationToken cancellationToken)
    {
        var exec = await _execRepo.GetByIdAsync(idExecution, cancellationToken);
        if (exec == null) return NotFound();
        var trazas = await _traceRepo.GetByExecutionAsync(idExecution, cancellationToken);
        return Ok(new
        {
            exec.IdExecution,
            AgentePrincipal = exec.AgentePrincipal?.Nombre,
            exec.Pregunta,
            exec.Estado,
            exec.RespuestaFinal,
            Pasos = exec.Pasos.Select(p => new
            {
                p.Orden,
                Agente = p.Agente?.Nombre,
                p.Accion,
                p.Resultado,
                p.TiempoMs,
                p.Estado
            }),
            Trazas = trazas.Select(t => new { t.Evento, t.Detalle, t.FechaHora })
        });
    }

    // ===== Administración de reglas de colaboración (Actividad 3) =====
    [HttpGet("reglas")]
    public async Task<ActionResult<List<AgentCollaborationRuleDto>>> GetReglas(CancellationToken cancellationToken)
    {
        var reglas = await _reglasRepo.GetAllAsync(cancellationToken);
        var agentes = await _asistenteRepo.GetAllAsync();
        var nombres = agentes.ToDictionary(a => a.IdAsistente, a => a.Nombre);
        var dtos = reglas.Select(r => new AgentCollaborationRuleDto
        {
            IdRule = r.IdRule,
            AgenteOrigen = r.AgenteOrigen,
            AgenteDestino = r.AgenteDestino,
            Permitido = r.Permitido,
            Prioridad = r.Prioridad,
            Activa = r.Activa,
            NombreOrigen = nombres.TryGetValue(r.AgenteOrigen, out var no) ? no : r.AgenteOrigen.ToString(),
            NombreDestino = nombres.TryGetValue(r.AgenteDestino, out var nd) ? nd : r.AgenteDestino.ToString()
        }).ToList();
        return Ok(dtos);
    }

    [HttpPost("reglas")]
    public async Task<ActionResult<AgentCollaborationRule>> CrearRegla(
        [FromBody] AgentCollaborationRule regla, CancellationToken cancellationToken)
    {
        regla.UsuarioCreacion = User.Identity?.Name;
        regla.FechaCreacion = DateTime.UtcNow;
        var creada = await _reglasRepo.AddAsync(regla, cancellationToken);
        return CreatedAtAction(nameof(GetReglas), null, creada);
    }

    [HttpPut("reglas/{id}")]
    public async Task<IActionResult> ActualizarRegla(int id, [FromBody] AgentCollaborationRule regla, CancellationToken cancellationToken)
    {
        var existente = await _reglasRepo.GetByIdAsync(id, cancellationToken);
        if (existente == null) return NotFound();
        existente.AgenteOrigen = regla.AgenteOrigen;
        existente.AgenteDestino = regla.AgenteDestino;
        existente.Permitido = regla.Permitido;
        existente.Prioridad = regla.Prioridad;
        existente.Activa = regla.Activa;
        await _reglasRepo.UpdateAsync(existente, cancellationToken);
        return NoContent();
    }

    [HttpDelete("reglas/{id}")]
    public async Task<IActionResult> EliminarRegla(int id, CancellationToken cancellationToken)
    {
        await _reglasRepo.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Agentes disponibles para construir reglas/colaboraciones.</summary>
    [HttpGet("agentes")]
    public async Task<ActionResult<List<object>>> Agentes(CancellationToken cancellationToken)
    {
        var agentes = await _asistenteRepo.GetAllAsync();
        return Ok(agentes.Select(a => new { a.IdAsistente, a.Nombre, a.Codigo, a.Estado }).ToList());
    }
}
