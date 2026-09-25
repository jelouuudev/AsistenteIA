using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Aprobaciones;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Entities.Aprobaciones;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Planner Engine (ETAPA 18). Orquesta Plan Builder, Plan Validator, Execution Graph Builder
/// y Execution Supervisor. Convierte lenguaje natural en un plan ejecutable y lo ejecuta
/// directamente a través de su propio Execution Graph (fuente de verdad).
/// NUNCA delega al Agent Orchestrator — el grafo que se visualiza es el que se ejecuta.
/// </summary>
public class PlannerEngine : IPlannerEngine
{
    private readonly PlanBuilder _builder;
    private readonly PlanValidator _validator;
    private readonly ExecutionGraphBuilder _graphBuilder;
    private readonly ExecutionSupervisor _supervisor;
    private readonly IPlanRepository _planRepo;
    private readonly IPlanStepRepository _stepRepo;
    private readonly IPlanDependencyRepository _depRepo;
    private readonly IPlanExecutionLogRepository _logRepo;
    private readonly IApprovalRequestRepository _reqRepo;
    private readonly IAsistenteRepository _asistenteRepo;
    private readonly ILogger<PlannerEngine> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApprovalManager _approvalManager;
    private readonly IToolOrchestrator _toolOrchestrator;
    private readonly IAgentOrchestrator _agentOrchestrator;
    // Semáforo para acceso a DbContext
    private readonly SemaphoreSlim _sem = new(1, 1);
    // Registry de CancellationTokenSource por ejecución activa (para cancelación real).
    private static readonly ConcurrentDictionary<int, CancellationTokenSource> _activeExecutions = new();

    public PlannerEngine(
        PlanBuilder builder,
        PlanValidator validator,
        ExecutionGraphBuilder graphBuilder,
        ExecutionSupervisor supervisor,
        IPlanRepository planRepo,
        IPlanStepRepository stepRepo,
        IPlanDependencyRepository depRepo,
        IPlanExecutionLogRepository logRepo,
        IApprovalRequestRepository reqRepo,
        IAsistenteRepository asistenteRepo,
        ILogger<PlannerEngine> logger,
        IServiceScopeFactory scopeFactory,
        ApprovalManager approvalManager,
        IToolOrchestrator toolOrchestrator,
        IAgentOrchestrator agentOrchestrator)
    {
        _builder = builder;
        _validator = validator;
        _graphBuilder = graphBuilder;
        _supervisor = supervisor;
        _planRepo = planRepo;
        _stepRepo = stepRepo;
        _depRepo = depRepo;
        _logRepo = logRepo;
        _reqRepo = reqRepo;
        _asistenteRepo = asistenteRepo;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _approvalManager = approvalManager;
        _toolOrchestrator = toolOrchestrator;
        _agentOrchestrator = agentOrchestrator;
    }

    public async Task<Plan> GenerarPlanAsync(string objetivo, int idUsuario, CancellationToken ct = default)
    {
        var plan = await _builder.ConstruirAsync(objetivo, idUsuario, ct);

        // Persistencia en UNA sola unidad: el Plan ya trae Pasos y Dependencias como
        // propiedades de navegación, así que un solo SaveChanges resuelve las FKs hijas.
        plan = await _planRepo.AddAsync(plan, ct);

        // Si el plan requiere aprobación, crear la solicitud inmediatamente.
        if (plan.RequiereAprobacion)
        {
            var pasosAprobacion = plan.Pasos.Where(p => p.Tipo == "Approval").ToList();
            var nombres = string.Join(", ", pasosAprobacion.Select(p => p.Nombre));
            var aprobadores = new List<int> { 1 }; // admin como aprobador principal por defecto
            var solicitud = await _approvalManager.CrearSolicitudAsync(
                plan.IdPlan,
                TipoAprobacion.Manual,
                plan.IdUsuario,
                $"Plan #{plan.IdPlan} requiere aprobación. Pasos: {nombres}",
                aprobadores,
                ct: ct);

            plan.Estado = "EnEsperaAprobacion";
            await _planRepo.UpdateAsync(plan, ct);
            await RegistrarLogAsync(plan.IdPlan, null, "PlanPausadoAprobacion",
                $"Plan pausado con {pasosAprobacion.Count} paso(s) Approval. Solicitud {solicitud.Codigo}.", ct);
        }

        await RegistrarLogAsync(plan.IdPlan, null, "PlanGenerado",
            $"Plan #{plan.IdPlan} generado con {plan.Pasos.Count} paso(s). Requiere aprobación: {plan.RequiereAprobacion}.", ct);
        return plan;
    }

    public async Task<ResultadoValidacionPlan> ValidarPlanAsync(Plan plan, CancellationToken ct = default)
    {
        var resultado = await _validator.ValidarAsync(plan, ct);
        plan.Estado = resultado.Valido ? "Validado" : "Borrador";
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarLogAsync(plan.IdPlan, null, "PlanValidado",
            resultado.Valido ? "Plan válido." : $"Plan inválido: {string.Join("; ", resultado.Errores)}", ct);
        return resultado;
    }

    public ExecutionGraph ConstruirGrafo(Plan plan) => _graphBuilder.Construir(plan);

    public async Task<AgentExecutionResult> EjecutarPlanAsync(int idPlan, CancellationToken ct = default)
    {
        var plan = await _planRepo.GetByIdAsync(idPlan, ct)
                   ?? throw new InvalidOperationException($"Plan {idPlan} no encontrado.");

        // Guarda anti-doble-ejecución: el controlador ya validó antes de invocar.
        // Solo rechazar si está realmente ejecutándose o esperando aprobación.
        if (plan.Estado == "EnEjecucion")
            throw new InvalidOperationException($"El plan {idPlan} ya está en ejecución.");
        if (plan.Estado == "EnEsperaAprobacion")
            throw new InvalidOperationException($"El plan {idPlan} ya tiene una solicitud de aprobación pendiente. Decida primero en el Centro de Aprobaciones.");

        var validacion = await ValidarPlanAsync(plan, ct);
        if (!validacion.Valido)
            throw new InvalidOperationException("El plan no pasó la validación: " + string.Join("; ", validacion.Errores));

        // Aprobación Human-in-the-Loop (ETAPA 19): la solicitud ya se creó al generar.
        // Si hay pasos Approval pero no hay solicitud pendiente, crear una.
        // Si ya hay solicitud pendiente, esperar resolución.
        var pasosAprobacion = plan.Pasos.Where(p => p.Tipo == "Approval").ToList();
        if (pasosAprobacion.Any())
        {
            var solicitudExistente = await _reqRepo.GetPendientesParaAsync(plan.IdUsuario, ct);
            var solicitudPlan = solicitudExistente.FirstOrDefault(s => s.IdPlan == plan.IdPlan);

            if (solicitudPlan == null)
            {
                // Crear solicitud si no existe
                var nombres = string.Join(", ", pasosAprobacion.Select(p => p.Nombre));
                var aprobadores = new List<int> { 1 };
                solicitudPlan = await _approvalManager.CrearSolicitudAsync(
                    plan.IdPlan, TipoAprobacion.Manual, plan.IdUsuario,
                    $"Plan #{plan.IdPlan} requiere aprobación. Pasos: {nombres}",
                    aprobadores, ct: ct);

                plan.Estado = "EnEsperaAprobacion";
                await _planRepo.UpdateAsync(plan, ct);
                await RegistrarLogAsync(plan.IdPlan, 0, "PlanPausadoAprobacion",
                    $"Plan pausado. Solicitud {solicitudPlan.Codigo}.", ct);
            }

            var resuelta = await _approvalManager.EsperarResolucionAsync(
                solicitudPlan.IdApproval, TimeSpan.FromHours(1), ct);

            if (resuelta.Estado != EstadoAprobacion.Aprobado)
            {
                await RegistrarLogAsync(plan.IdPlan, 0, "PlanNoAprobado",
                    $"Solicitud {solicitudPlan.Codigo} resolvió como {resuelta.Estado}. Plan no ejecutado.", ct);
                return new AgentExecutionResult
                {
                    Estado = "Cancelado",
                    Exitoso = false,
                    Error = $"Plan no aprobado: {resuelta.Estado}"
                };
            }
        }
        else if (plan.RequiereAprobacion && !plan.Aprobado)
        {
            throw new InvalidOperationException("El plan requiere aprobación humana antes de ejecutarse.");
        }

        // === EJECUCIÓN DEL GRAFO (fuente de verdad) ===
        // El grafo validado se ejecuta NODO POR NODO a través del Agent Orchestrator
        // (EjecutarPasoValidadoAsync): el Orchestrator ejecuta exactamente los pasos
        // del plan, sin re-seleccionar agentes ni reconstruir el grafo.

        // 1. Ejecutar paso Coordination vía Orchestrator (texto fijo, sin LLM)
        var todosLosPasos = await _stepRepo.GetByPlanAsync(plan.IdPlan, ct);
        var pasoCoordinacion = todosLosPasos.FirstOrDefault(p => p.Tipo == "Coordination");
        if (pasoCoordinacion != null)
        {
            var resCoord = await _agentOrchestrator.EjecutarPasoValidadoAsync(
                plan, pasoCoordinacion, null, null, plan.IdUsuario, ct);
            pasoCoordinacion.Resultado = resCoord.Resultado;
            pasoCoordinacion.Estado = "Completado";
            await _stepRepo.UpdateAsync(pasoCoordinacion, ct);
            await RegistrarLogAsync(plan.IdPlan, pasoCoordinacion.Orden, "PasoCoordinacion",
                "Plan de acción generado por el coordinador (vía Orchestrator).", ct);
        }

        // 2. Ejecutar pasos Tool y RAG VÍA EL ORCHESTRATOR (fuente de verdad del DAG):
        // el Planner aporta validación, retry por nodo (supervisor) y auditoría; la
        // ejecución de cada paso la realiza el Agent Orchestrator (EjecutarPasoValidadoAsync).
        // Ante fallo, el supervisor decide reintento; si se agota, el paso queda en Error
        // y el resto del grafo continúa.
        var pasosTool = todosLosPasos.Where(p => p.Tipo == "Tool" || p.Tipo == "RAG").ToList();
        string? resultadoAnterior = null;
        foreach (var paso in pasosTool)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                bool exitoPaso = false;
                try
                {
                    var resOrch = await _agentOrchestrator.EjecutarPasoValidadoAsync(
                        plan, paso, null, resultadoAnterior, plan.IdUsuario, ct);

                    if (resOrch.Exito)
                    {
                        paso.Resultado = resOrch.Resultado;
                        paso.Estado = "Completado";
                        await _stepRepo.UpdateAsync(paso, ct);
                        resultadoAnterior = resOrch.Resultado;
                        await RegistrarLogAsync(plan.IdPlan, paso.Orden, "PasoToolEjecutado",
                            $"'{paso.CodigoHerramienta ?? paso.Tipo}' ejecutó '{paso.Nombre}' con éxito (vía Orchestrator).", ct);
                        exitoPaso = true;
                    }
                    else
                    {
                        paso.Resultado = resOrch.Error;
                        await _stepRepo.UpdateAsync(paso, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Planner: fallo al ejecutar '{Herramienta}' para el paso {Paso}", paso.CodigoHerramienta, paso.Nombre);
                    paso.Resultado = $"Error al ejecutar {paso.CodigoHerramienta}: " + ex.Message;
                    await _stepRepo.UpdateAsync(paso, ct);
                }

                if (exitoPaso) break;

                // Fallo: el supervisor decide si se reintenta el MISMO nodo.
                bool reintentar;
                try
                {
                    reintentar = await _supervisor.ManejarFalloPasoAsync(plan, paso, paso.Resultado ?? "Error en paso Tool.", ct);
                }
                catch (OperationCanceledException)
                {
                    paso.Estado = "Cancelado";
                    await _stepRepo.UpdateAsync(paso, CancellationToken.None);
                    break;
                }
                if (!reintentar)
                {
                    await RegistrarLogAsync(plan.IdPlan, paso.Orden, "PasoToolSinDatos",
                        $"{paso.CodigoHerramienta}: {paso.Resultado}", ct);
                    break;
                }
            }
        }

        // 3. Construir el ExecutionGraph desde el Plan (fuente de verdad)
        var grafo = _graphBuilder.Construir(plan);
        var capas = grafo.ObtenerCapas();

        // Marcar ejecución en curso
        plan.Estado = "EnEjecucion";
        await _planRepo.UpdateAsync(plan, ct);

        // 4. Ejecutar capas en background (fire-and-forget) con CTS registrado
        // para cancelación real (B-03): el endpoint Cancelar invoca Cancel().
        var idPlanLocal = plan.IdPlan;
        var execCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeExecutions[idPlanLocal] = execCts;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var stepRepo = scope.ServiceProvider.GetRequiredService<IPlanStepRepository>();
                var planRepo = scope.ServiceProvider.GetRequiredService<IPlanRepository>();

                // Ejecutar capas secuencialmente, nodos dentro de cada capa en paralelo
                foreach (var capa in capas)
                {
                    execCts.Token.ThrowIfCancellationRequested();
                    var tareasCapa = capa
                        .Where(n => n.Estado != "Completado")
                        .Select(n => EjecutarNodoGrafoAsync(n, plan, stepRepo, execCts.Token))
                        .ToArray();

                    if (tareasCapa.Any())
                        await Task.WhenAll(tareasCapa);
                }

                // Actualizar estado del Plan a Completado (salvo cancelación: si algún
                // paso quedó Cancelado, el plan es Cancelado aunque el grafo haya terminado).
                var planFinal = await planRepo.GetByIdAsync(idPlanLocal, CancellationToken.None);
                if (planFinal != null && planFinal.Estado == "EnEjecucion")
                {
                    planFinal.Estado = planFinal.Pasos.Any(p => p.Estado == "Cancelado")
                        ? "Cancelado"
                        : "Completado";
                    planFinal.FechaFin = DateTime.UtcNow;
                    await planRepo.UpdateAsync(planFinal, CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelación solicitada por el usuario: marcar Cancelado, no Fallido.
                try {
                    await using var scopeC = _scopeFactory.CreateAsyncScope();
                    var planRepoC = scopeC.ServiceProvider.GetRequiredService<IPlanRepository>();
                    var planC = await planRepoC.GetByIdAsync(idPlanLocal, CancellationToken.None);
                    if (planC != null && planC.Estado == "EnEjecucion")
                    {
                        planC.Estado = "Cancelado";
                        planC.FechaFin = DateTime.UtcNow;
                        await planRepoC.UpdateAsync(planC, CancellationToken.None);
                    }
                    await RegistrarLogAsync(idPlanLocal, null, "PlanCancelado", "Ejecución interrumpida por cancelación.", CancellationToken.None);
                } catch { }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ejecución del grafo para plan {IdPlan}", idPlanLocal);
                try {
                    await using var scopeErr = _scopeFactory.CreateAsyncScope();
                    var planRepoErr = scopeErr.ServiceProvider.GetRequiredService<IPlanRepository>();
                    var planErr = await planRepoErr.GetByIdAsync(idPlanLocal, CancellationToken.None);
                    if (planErr != null) { planErr.Estado = "Fallido"; await planRepoErr.UpdateAsync(planErr, CancellationToken.None); }
                } catch { }
            }
            finally
            {
                if (_activeExecutions.TryRemove(idPlanLocal, out var reg)) reg.Dispose();
            }
        });

        return new AgentExecutionResult
        {
            IdExecution = 0, // No se usa AgentExecution
            Estado = "EnEjecucion",
            Exitoso = false
        };
    }

    /// <summary>
    /// Cancelación real (B-03): detiene el trabajo activo del plan invocando Cancel()
    /// y marca el estado. No se limita a cambiar el estado como antes.
    /// </summary>
    public async Task<bool> CancelarEjecucionAsync(int idPlan, CancellationToken ct = default)
    {
        // 1. Detener trabajo activo si existe.
        if (_activeExecutions.TryGetValue(idPlan, out var cts))
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        // 2. Marcar estado aunque ya no esté activo (encolado, en espera, etc.).
        await using var scope = _scopeFactory.CreateAsyncScope();
        var planRepo = scope.ServiceProvider.GetRequiredService<IPlanRepository>();
        var plan = await planRepo.GetByIdAsync(idPlan, ct);
        if (plan == null) return false;

        if (plan.Estado == "EnEjecucion" || plan.Estado == "EnEsperaAprobacion" || plan.Estado == "Borrador")
        {
            plan.Estado = "Cancelado";
            plan.FechaFin = DateTime.UtcNow;
            await planRepo.UpdateAsync(plan, ct);
        }
        await RegistrarLogAsync(idPlan, null, "PlanCancelado", "Cancelado por el usuario.", ct);
        return true;
    }

    /// <summary>
    /// Ejecuta un nodo del grafo con retry por nodo (ítem 6): ante Error, el supervisor
    /// decide reintento del MISMO nodo según política; la cancelación no se reintenta.
    /// </summary>
    private async Task EjecutarNodoGrafoAsync(ExecutionNode nodo, Plan plan, IPlanStepRepository stepRepo, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await EjecutarNodoUnaVezAsync(nodo, plan, stepRepo, ct);

            var paso = plan.Pasos.FirstOrDefault(p => p.Orden == nodo.IdNodo);
            if (paso == null || paso.Estado == "Completado" || paso.Estado == "Cancelado" || paso.Estado == "Omitido")
                return;
            if (paso.Estado != "Error")
                return;

            bool reintentar;
            try
            {
                // Supervisor con scope propio (el background no debe usar repos del request).
                await using var scope = _scopeFactory.CreateAsyncScope();
                var supervisor = scope.ServiceProvider.GetRequiredService<ExecutionSupervisor>();
                reintentar = await supervisor.ManejarFalloPasoAsync(plan, paso, paso.Resultado ?? "Error en el nodo.", ct);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    paso.Estado = "Cancelado";
                    await stepRepo.UpdateAsync(paso, CancellationToken.None);
                }
                catch { }
                return;
            }
            if (!reintentar) return;
        }
    }

    /// <summary>Ejecuta un nodo del grafo directamente (sin Orchestrator), un solo intento.</summary>
    private async Task EjecutarNodoUnaVezAsync(ExecutionNode nodo, Plan plan, IPlanStepRepository stepRepo, CancellationToken ct)
    {
        var paso = plan.Pasos.FirstOrDefault(p => p.Orden == nodo.IdNodo);
        if (paso == null) return;

        // Si es paso Tool o Coordination, ya fue ejecutado arriba
        if (paso.Tipo == "Tool" || paso.Tipo == "Coordination" || paso.Tipo == "RAG") return;

        // Aprobación vía Orchestrator (la gestiona ApprovalManager; aquí se marca Omitido).
        if (paso.Tipo == "Approval")
        {
            var resApr = await _agentOrchestrator.EjecutarPasoValidadoAsync(
                plan, paso, null, null, plan.IdUsuario, ct);
            if (resApr.Omitido)
            {
                paso.Estado = "Omitido";
                await stepRepo.UpdateAsync(paso, ct);
            }
            return;
        }

        // Si es paso Workflow, ejecutarlo vía Orchestrator con el IdWorkflow del paso.
        // Antes estos pasos se validaban pero nunca se ejecutaban (quedaban pendientes para siempre).
        if (paso.Tipo == "Workflow")
        {
            await _sem.WaitAsync(ct);
            try
            {
                var resWf = await _agentOrchestrator.EjecutarPasoValidadoAsync(
                    plan, paso, null, null, plan.IdUsuario, ct);

                if (resWf.Exito)
                {
                    paso.Resultado = resWf.Resultado;
                    paso.Estado = "Completado";
                }
                else
                {
                    paso.Estado = "Error";
                    paso.Resultado = resWf.Error ?? "El workflow no devolvió resultado.";
                }
                await stepRepo.UpdateAsync(paso, ct);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    paso.Estado = "Cancelado";
                    paso.Resultado = "Ejecución cancelada o timeout";
                    await stepRepo.UpdateAsync(paso, ct);
                }
                catch { }
            }
            catch (Exception ex)
            {
                try
                {
                    paso.Estado = "Error";
                    paso.Resultado = $"Error: {ex.Message}";
                    await stepRepo.UpdateAsync(paso, ct);
                }
                catch { }
            }
            finally
            {
                _sem.Release();
            }
            return;
        }

        // Si es paso Agent, ejecutarlo vía Orchestrator (Agent Runtime con contexto previo).
        if (paso.Tipo == "Agent")
        {
            await _sem.WaitAsync(ct);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

                // Inyectar resultados de pasos anteriores
                var pasosAnteriores = (await stepRepo.GetByPlanAsync(plan.IdPlan, ct, true))
                    .Where(p => p.Orden < paso.Orden && !string.IsNullOrWhiteSpace(p.Resultado))
                    .Select(p => $"[Paso {p.Orden}: {p.Nombre}]\n{p.Resultado}");
                var contextoPrevio = string.Join("\n", pasosAnteriores);

                // Timeout más largo para CPU (15 min)
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

                // Ejecución vía Orchestrator (fuente de verdad del DAG).
                var resAg = await _agentOrchestrator.EjecutarPasoValidadoAsync(
                    plan, paso, contextoPrevio, null, plan.IdUsuario, linkedCts.Token);

                if (resAg.Exito)
                {
                    paso.Resultado = resAg.Resultado;
                    paso.Estado = "Completado";
                    await stepRepo.UpdateAsync(paso, ct);
                }
                else
                {
                    paso.Estado = "Error";
                    paso.Resultado = resAg.Error ?? "Sin respuesta del agente";
                    await stepRepo.UpdateAsync(paso, ct);
                }
            }
            catch (OperationCanceledException)
            {
                try {
                    paso.Estado = "Cancelado";
                    paso.Resultado = "Ejecución cancelada o timeout";
                    await stepRepo.UpdateAsync(paso, ct);
                } catch { }
            }
            catch (Exception ex)
            {
                try {
                    paso.Estado = "Error";
                    paso.Resultado = $"Error: {ex.Message}";
                    await stepRepo.UpdateAsync(paso, ct);
                } catch { }
            }
            finally
            {
                _sem.Release();
            }
        }
    }

    public async Task<SimulacionPlan> SimularAsync(int idPlan, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepo.GetByIdAsync(idPlan, cancellationToken)
                   ?? throw new InvalidOperationException($"Plan {idPlan} no encontrado.");

        // Validación en seco (Regla 1 / Actividad 3) — sin ejecutar nada.
        var validacion = await _validator.ValidarAsync(plan, cancellationToken);

        // Predicción: participantes (agentes reales) y herramientas que intervendrían.
        var agentes = (await _asistenteRepo.GetAllAsync()).ToDictionary(a => a.IdAsistente, a => a.Nombre ?? a.Codigo);
        var participantes = plan.Pasos
            .Where(p => p.IdAsistente.HasValue)
            .Select(p => agentes.TryGetValue(p.IdAsistente!.Value, out var nombre)
                ? nombre
                : $"Agente {p.IdAsistente}")
            .Distinct()
            .ToList();
        var herramientas = plan.Pasos
            .Where(p => !string.IsNullOrWhiteSpace(p.CodigoHerramienta))
            .Select(p => p.CodigoHerramienta!)
            .Distinct()
            .ToList();

        // Estimación simple en CPU (~60 s por paso de agente/herramienta, DeepSeek-r1:7b).
        int tiempoEstimado = plan.Pasos.Count * 60;

        return new SimulacionPlan
        {
            Plan = plan,
            Validacion = validacion,
            Participantes = participantes,
            Herramientas = herramientas,
            TiempoEstimadoSegundos = tiempoEstimado
        };
    }

    /// <summary>
    /// Genera un texto de coordinación que describe el plan de acción.
    /// Se usa para el paso 0 (Coordination) en vez de invocar al LLM.
    /// </summary>
    private string GenerarTextoCoordinacion(Plan plan)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("**Plan de acción coordinado:**");
        sb.AppendLine();
        sb.AppendLine($"He analizado tu solicitud: \"{plan.Objetivo}\".");
        sb.AppendLine("He diseñado un plan de trabajo con los siguientes pasos:");
        sb.AppendLine();

        foreach (var paso in plan.Pasos.OrderBy(p => p.Orden))
        {
            var tipoLabel = paso.Tipo switch
            {
                "Tool" => "🛠️",
                "Agent" => "🤖",
                "RAG" => "📚",
                "Approval" => "✅",
                _ => "📋"
            };
            sb.AppendLine($"- {tipoLabel} **Paso {paso.Orden}:** {paso.Nombre}");
        }

        sb.AppendLine();
        sb.AppendLine("Cada paso será ejecutado en orden, y los resultados se consolidarán para entregarte la respuesta final.");
        sb.AppendLine();
        sb.AppendLine($"⏱️ Tiempo estimado: ~{Math.Max(1, plan.Pasos.Count / 2)} minutos en CPU local.");
        sb.AppendLine("🔒 Tus datos permanecen en tu infraestructura (IA local, sin nube).");

        return sb.ToString();
    }

    public async Task RegistrarLogAsync(int idPlan, int? idStep, string evento, string? detalle, CancellationToken ct = default)
        => await _logRepo.AddAsync(new PlanExecutionLog
        {
            IdPlan = idPlan,
            IdStep = idStep,
            Evento = evento,
            Detalle = detalle,
            Fecha = DateTime.UtcNow
        }, ct);
}
