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

    // Huella del grafo que se está ejecutando. Viaja hasta el Orchestrator para que
    // cada nodo se ejecute como unidad del DAG validado y quede registrada su
    // procedencia (misma huella que devuelve /api/planner/simular y que se
    // persistió en GrafoValidado).
    private string _huellaGrafoEjecucion = string.Empty;
    // Resultados en memoria de esta ejecución (Orden → Resultado), para que los
    // pasos de abajo (reporte/entrega) no dependan de releer filas que puedan
    // haberse degradado en BD. Una instancia por ejecución (scoped).
    private readonly ConcurrentDictionary<int, string> _memoriaResultados = new();

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
        // No degradar estados activos: una re-validación en caliente no debe reabrir
        // la ventana anti-doble-ejecución (era la causa de lanzamientos duplicados).
        if (!resultado.Valido)
        {
            plan.Estado = "Borrador";
        }
        else if (plan.Estado is "Borrador" or "Validado" or "Fallido" or "Completado" or "Cancelado")
        {
            plan.Estado = "Validado";
        }
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarLogAsync(plan.IdPlan, null, "PlanValidado",
            resultado.Valido ? "Plan válido." : $"Plan inválido: {string.Join("; ", resultado.Errores)}", ct);

        // Solo si es válido se construye y registra el grafo: es el que después se
        // ejecutará. La huella se coteja contra la del log GrafoEjecutado.
        if (resultado.Valido)
        {
            var grafoValido = ConstruirGrafo(plan);
            var capasValidas = grafoValido.ObtenerCapas();
            await RegistrarLogAsync(plan.IdPlan, null, "GrafoValidado",
                $"DAG validado: {grafoValido.Nodos.Count} nodos en {capasValidas.Count} capas. " +
                $"Huella {grafoValido.CalcularHuella()}.", ct);
        }
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

            // Pausa hasta decisión humana. La reanudación es POR EVENTO (Decidir →
            // relanza la ejecución): no se espera en memoria porque un reinicio
            // huérfana el plan. El background termina aquí.
            await RegistrarLogAsync(plan.IdPlan, 0, "PlanEnEsperaAprobacion",
                $"Solicitud {solicitudPlan.Codigo} pendiente de decisión humana.", ct);
            return new AgentExecutionResult
            {
                Estado = "EnEsperaAprobacion",
                Exitoso = true
            };
        }
        else if (plan.RequiereAprobacion && !plan.Aprobado)
        {
            throw new InvalidOperationException("El plan requiere aprobación humana antes de ejecutarse.");
        }

        // === EJECUCIÓN DEL GRAFO (fuente de verdad) ===
        // El grafo validado se ejecuta NODO POR NODO a través del Agent Orchestrator
        // (EjecutarPasoValidadoAsync): el Orchestrator ejecuta exactamente los pasos
        // del plan, sin re-seleccionar agentes ni reconstruir el grafo.
        await LanzarEjecucionGrafo(plan, ct);

        return new AgentExecutionResult
        {
            IdExecution = 0, // No se usa AgentExecution
            Estado = "EnEjecucion",
            Exitoso = false
        };
    }

    /// <summary>
    /// Continúa un plan pausado por aprobación ya resuelta (relanzado por evento desde
    /// ApprovalManager). Valida estado y evita doble ejecución.
    /// </summary>
    public async Task ContinuarPlanAprobadoAsync(int idPlan, CancellationToken ct = default)
    {
        var plan = await _planRepo.GetByIdAsync(idPlan, ct)
                   ?? throw new InvalidOperationException($"Plan {idPlan} no encontrado.");

        if (plan.Estado != "EnEsperaAprobacion" || !plan.Aprobado)
        {
            _logger.LogInformation("ContinuarPlanAprobadoAsync: plan {Id} en estado {Estado} (Aprobado={Aprobado}); no se relanza.",
                idPlan, plan.Estado, plan.Aprobado);
            return;
        }

        if (_activeExecutions.ContainsKey(idPlan))
        {
            _logger.LogInformation("ContinuarPlanAprobadoAsync: plan {Id} ya tiene ejecución activa; no se duplica.", idPlan);
            return;
        }

        await LanzarEjecucionGrafo(plan, ct);
    }

    /// <summary>
    /// Ejecuta las fases inline (Coordinación + Tool/RAG vía Orchestrator) y luego
    /// lanza el grafo validado en background. Fuente única de lanzamiento
    /// (vía normal y vía reanudación por evento).
    /// </summary>
    private async Task LanzarEjecucionGrafo(Plan plan, CancellationToken ct)
    {
        // B-03: el CTS se registra AQUÍ, antes de la fase pesada (era el bug:
        // se registraba en la fase B de background, así que durante los 60-90s
        // de SQL/RAG inline no existía y el cancel no encontraba nada). El
        // token de ejecución cubre ambas fases; el del request solo se usa
        // como padre enlazado.
        var idPlanLocalInicio = plan.IdPlan;
        var execCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeExecutions[idPlanLocalInicio] = execCts;
        var execToken = execCts.Token;

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
        // Capas por dependencias: los pasos sin dependencias entre sí (ej. ramas
        // paralelas de consulta + RAG) corren en paralelo con scopes propios
        // (DbContext aislado por tarea). Ante fallo, el supervisor decide reintento;
        // si se agota, el paso queda en Error y el resto del grafo continúa.
        var pasosTool = todosLosPasos.Where(p => p.Tipo == "Tool" || p.Tipo == "RAG").ToList();
        foreach (var capa in CalcularCapasTool(pasosTool, plan.Dependencias))
        {
            execToken.ThrowIfCancellationRequested();
            // Backstop por estado con scope fresco (mismo motivo que en la fase
            // B: el repositorio es tracking y el request puede haber marcado
            // Cancelado mientras esta capa estaba en vuelo).
            await using (var scopeCapa = _scopeFactory.CreateAsyncScope())
            {
                var repoCapa = scopeCapa.ServiceProvider.GetRequiredService<IPlanRepository>();
                if ((await repoCapa.GetByIdAsync(plan.IdPlan, CancellationToken.None))?.Estado == "Cancelado")
                    throw new OperationCanceledException($"Plan {plan.IdPlan} cancelado (detectado por estado en fase A).");
            }
            var tareas = capa.Select(paso => EjecutarPasoToolConReintentoAsync(paso, plan, execToken)).ToArray();
            if (tareas.Length > 0)
                await Task.WhenAll(tareas);
        }

        // Funciones locales: capas topológicas (misma capa = paralelo seguro) y
        // ejecución de un paso con reintentos, todo en scope propio.
        static List<List<PlanStep>> CalcularCapasTool(List<PlanStep> pasos, ICollection<PlanDependency> dependencias)
    {
        var ordenes = new HashSet<int>(pasos.Select(p => p.Orden));
        var nivel = pasos.ToDictionary(p => p.Orden, _ => 0);
        bool cambio = true;
        while (cambio)
        {
            cambio = false;
            foreach (var d in dependencias.Where(d => ordenes.Contains(d.StepOrigen) && ordenes.Contains(d.StepDestino)))
            {
                if (nivel[d.StepDestino] <= nivel[d.StepOrigen])
                {
                    nivel[d.StepDestino] = nivel[d.StepOrigen] + 1;
                    cambio = true;
                }
            }
        }
        return pasos.GroupBy(p => nivel[p.Orden]).OrderBy(g => g.Key)
            .Select(g => g.OrderBy(p => p.Orden).ToList()).ToList();
    }

    /// <summary>
    /// Ejecuta un paso Tool/RAG con reintentos del supervisor, todo en scope propio
    /// (seguro para ejecución paralela entre ramas: sin DbContext compartido).
    /// </summary>
    async Task EjecutarPasoToolConReintentoAsync(PlanStep pasoOrigen, Plan plan, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var stepRepo = sp.GetRequiredService<IPlanStepRepository>();
        var supervisor = sp.GetRequiredService<ExecutionSupervisor>();
        var logRepo = sp.GetRequiredService<IPlanExecutionLogRepository>();
        _logger.LogInformation("PlannerTask: paso {Orden} (IdStep {Id}) en scope propio.", pasoOrigen.Orden, pasoOrigen.IdStep);

        async Task LogAsync(string evento, string? detalle)
        {
            try
            {
                await logRepo.AddAsync(new PlanExecutionLog
                {
                    IdPlan = plan.IdPlan,
                    IdStep = pasoOrigen.IdStep,
                    Evento = evento,
                    Detalle = detalle,
                    Fecha = DateTime.UtcNow
                }, ct);
            }
            catch { }
        }

        // Entidad fresca del scope propio (la de la petición no debe tocarse en paralelo).
        var paso = await stepRepo.GetByIdAsync(pasoOrigen.IdStep, ct) ?? pasoOrigen;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            bool exitoPaso = false;
            try
            {
                // Handoff en memoria: los pasos Tool/RAG reciben lo acumulado por
                // ramas previas (el ReportTool lo prefiere sobre releer la BD).
                var resOrch = await _agentOrchestrator.EjecutarPasoValidadoAsync(
                    plan, paso, null, CombinarMemoria(paso.Orden), plan.IdUsuario, ct);

                if (resOrch.Exito)
                {
                    paso.Resultado = resOrch.Resultado;
                    paso.Estado = "Completado";
                    await stepRepo.UpdateAsync(paso, ct);
                    // Handoff en memoria para capas posteriores (no depender de la BD).
                    if (!string.IsNullOrWhiteSpace(resOrch.Resultado))
                        _memoriaResultados[paso.Orden] = resOrch.Resultado;
                    // Verificación REAL con scope fresco y sin tracking: el repositorio
                    // comparte contexto con la escritura y siempre diría que sí.
                    try
                    {
                        await using var scopeVerif = _scopeFactory.CreateAsyncScope();
                        var verifRepo = scopeVerif.ServiceProvider.GetRequiredService<IPlanStepRepository>();
                        var verificado = (await verifRepo.GetByPlanAsync(plan.IdPlan, ct, true))
                            .FirstOrDefault(s => s.Orden == paso.Orden);
                        _logger.LogInformation("PlannerTask: verificación paso {Orden}: Estado={Estado}, LenRes={Len}.",
                            paso.Orden, verificado?.Estado, verificado?.Resultado?.Length ?? -1);
                        if (verificado == null || verificado.Estado != "Completado" || string.IsNullOrWhiteSpace(verificado.Resultado))
                        {
                            _logger.LogError("Planner: el paso {Orden} NO persistió su resultado (verificación post-Update falló).", paso.Orden);
                        }
                    }
                    catch (Exception exVerif)
                    {
                        _logger.LogWarning(exVerif, "Planner: no se pudo verificar la persistencia del paso {Orden}.", paso.Orden);
                    }
                    await LogAsync("PasoToolEjecutado",
                        $"'{paso.CodigoHerramienta ?? paso.Tipo}' ejecutó '{paso.Nombre}' con éxito (vía Orchestrator).");
                    exitoPaso = true;
                }
                else
                {
                    paso.Resultado = resOrch.Error;
                    await stepRepo.UpdateAsync(paso, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Planner: fallo al ejecutar '{Herramienta}' para el paso {Paso}", paso.CodigoHerramienta, paso.Nombre);
                paso.Resultado = $"Error al ejecutar {paso.CodigoHerramienta}: " + ex.Message;
                try { await stepRepo.UpdateAsync(paso, ct); } catch { }
            }

            if (exitoPaso) break;

            // Fallo: el supervisor decide si se reintenta el MISMO nodo.
            bool reintentar;
            try
            {
                reintentar = await supervisor.ManejarFalloPasoAsync(plan, paso, paso.Resultado ?? "Error en paso Tool.", ct);
            }
            catch (OperationCanceledException)
            {
                paso.Estado = "Cancelado";
                try { await stepRepo.UpdateAsync(paso, CancellationToken.None); } catch { }
                break;
            }
            if (!reintentar)
            {
                await LogAsync("PasoToolSinDatos", $"{paso.CodigoHerramienta}: {paso.Resultado}");
                break;
            }
        }
    }

        // 3. Construir el ExecutionGraph desde el Plan (fuente de verdad)
        var grafo = _graphBuilder.Construir(plan);
        var capas = grafo.ObtenerCapas();

        // Prueba de que el DAG ejecutado es el validado: se registra la huella del
        // grafo que se va a ejecutar. /api/planner/simular devuelve la huella del
        // MISMO grafo (mismo builder, mismo plan). Si coinciden, no hayGraph rebuild
        // ni re-selección entre validar y ejecutar.
        var huellaGrafo = grafo.CalcularHuella();
        _huellaGrafoEjecucion = huellaGrafo;
        _logger.LogInformation("Planner: ejecutando plan {IdPlan} con el grafo {Huella} ({Nodos} nodos, {Capas} capas).",
            plan.IdPlan, huellaGrafo, grafo.Nodos.Count, capas.Count);
        await RegistrarLogAsync(plan.IdPlan, null, "GrafoEjecutado",
            $"Grafo validado: {grafo.Nodos.Count} nodos en {capas.Count} capas. Huella {huellaGrafo}.", ct);

        // B-03 (carrera validación-vs-cancel): la validación previa tarda 20+s en
        // CPU y el usuario puede cancelar en ese ventana, ANTES de que exista el
        // CTS. Si al llegar aquí el plan ya está Cancelado, no se lanza nada y
        // sobre todo no se pisa el estado con EnEjecucion. Sin esta guarda, el
        // lanzamiento borraba el Cancelado y el plan corría entero (plan #13237:
        // cancelado a los 25s, completado a los 100s). Lectura con scope fresco:
        // el repositorio es tracking y devolvería el estado stale.
        string? estadoPrevio;
        await using (var scopePrevio = _scopeFactory.CreateAsyncScope())
        {
            var repoPrevio = scopePrevio.ServiceProvider.GetRequiredService<IPlanRepository>();
            estadoPrevio = (await repoPrevio.GetByIdAsync(plan.IdPlan, CancellationToken.None))?.Estado;
        }
        if (estadoPrevio == "Cancelado")
        {
            _logger.LogInformation("Planner: plan {IdPlan} cancelado durante la validación; no se lanza la ejecución.", plan.IdPlan);
            await RegistrarLogAsync(plan.IdPlan, null, "PlanCancelado", "Cancelado antes de iniciar la ejecución.", ct);
            return;
        }

        // Marcar ejecución en curso (solo escalar: jamás tocar filas de pasos aquí,
        // los resultados de la sección 2 ya están persistidos por sus scopes).
        // Tampoco se pisa un estado terminal que otro flujo haya fijado.
        if (estadoPrevio == "Fallido" || estadoPrevio == "Completado") return;
        await _planRepo.UpdateEstadoAsync(plan.IdPlan, "EnEjecucion", null, ct);

        // 4. Ejecutar capas en background (fire-and-forget) con el MISMO CTS
        // registrado al inicio (B-03): crear otro aquí lo reemplazaría en el
        // diccionario y el cancel golpearía al equivocado. El endpoint Cancelar
        // invoca Cancel() sobre este.
        var idPlanLocal = plan.IdPlan;
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
                    // Backstop por estado (B-03): si el CTS no existe o no se
                    // propagó (carrera con la validación, reinicio, doble
                    // lanzamiento), el estado Cancelado en BD es la segunda
                    // señal y también detiene. Se lee con SCOPE FRESCO en cada
                    // capa: el repositorio es tracking a propósito y devolvería
                    // la entidad stale (EnEjecucion) para siempre.
                    string? estadoCapa;
                    await using (var scopeEstado = _scopeFactory.CreateAsyncScope())
                    {
                        var repoEstado = scopeEstado.ServiceProvider.GetRequiredService<IPlanRepository>();
                        estadoCapa = (await repoEstado.GetByIdAsync(idPlanLocal, CancellationToken.None))?.Estado;
                    }
                    if (estadoCapa == "Cancelado")
                        throw new OperationCanceledException($"Plan {idPlanLocal} cancelado (detectado por estado).");
                    var tareasCapa = capa
                        .Where(n => n.Estado != "Completado")
                        .Select(n => EjecutarNodoGrafoAsync(n, plan, stepRepo, execCts.Token))
                        .ToArray();

                    if (tareasCapa.Any())
                        await Task.WhenAll(tareasCapa);
                }

                // Estado final del Plan. UPDATE escalar directo: jamás toca filas de pasos.
                // Se acepta también IniciandoEjecucion: si el marcado a EnEjecucion se
                // perdió (o el plan viene de una versión anterior), igual debe cerrarse.
                var planFinal = await planRepo.GetByIdAsync(idPlanLocal, CancellationToken.None);
                // Un Cancelado nunca se sobrescribe con Completado: si el usuario
                // canceló (o el backstop por estado abortó capas), el final es
                // Cancelado aunque algún nodo tardío haya terminado. La lectura
                // es con scope fresco por el tracking del repositorio.
                string? estadoFinalLeido;
                await using (var scopeFinal = _scopeFactory.CreateAsyncScope())
                {
                    var repoFinal = scopeFinal.ServiceProvider.GetRequiredService<IPlanRepository>();
                    estadoFinalLeido = (await repoFinal.GetByIdAsync(idPlanLocal, CancellationToken.None))?.Estado;
                }
                if (planFinal != null && estadoFinalLeido == "Cancelado")
                {
                    await RegistrarLogAsync(idPlanLocal, null, "PlanCancelado", "Ejecución interrumpida por cancelación.", CancellationToken.None);
                }
                else if (planFinal != null && (planFinal.Estado == "EnEjecucion" || planFinal.Estado == "IniciandoEjecucion"))
                {
                    // Cancelado > Fallido > Completado. Un plan con pasos en Error NO es
                    // "Completado": en el #6038 los dos pasos SqlQueryTool agotaron
                    // reintentos y el plan se anunciado como Completado igual, lo que
                    // ocultaba el fallo real detrás de una entrega final que además
                    // se inventó los datos.
                    var estadoFinal = planFinal.Pasos.Any(p => p.Estado == "Cancelado")
                        ? "Cancelado"
                        : planFinal.Pasos.Any(p => p.Estado == "Error")
                            ? "Fallido"
                            : "Completado";
                    await planRepo.UpdateEstadoAsync(idPlanLocal, estadoFinal, DateTime.UtcNow, CancellationToken.None);
                    var logRepo = scope.ServiceProvider.GetRequiredService<IPlanExecutionLogRepository>();
                    var fallidos = planFinal.Pasos.Where(p => p.Estado == "Error").Select(p => p.Nombre).ToList();
                    try
                    {
                        await logRepo.AddAsync(new PlanExecutionLog
                        {
                            IdPlan = idPlanLocal,
                            Evento = estadoFinal switch
                            {
                                "Completado" => "PlanCompletado",
                                "Cancelado" => "PlanCancelado",
                                _ => "PlanFallido"
                            },
                            Detalle = estadoFinal switch
                            {
                                "Completado" => $"Plan #{idPlanLocal} completado con {planFinal.Pasos.Count} paso(s).",
                                "Cancelado" => $"Plan #{idPlanLocal} finalizado con pasos cancelados.",
                                _ => $"Plan #{idPlanLocal} finalizado con {fallidos.Count} paso(s) en error: {string.Join(" | ", fallidos)}."
                            },
                            Fecha = DateTime.UtcNow
                        }, CancellationToken.None);
                    }
                    catch { }
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
                        await planRepoC.UpdateEstadoAsync(idPlanLocal, "Cancelado", DateTime.UtcNow, CancellationToken.None);
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
                    // Un Cancelado NUNCA se convierte en Fallido: si el usuario canceló
                    // y luego el nodo lanza una excepción no-OCE (consulta SQL abortada,
                    // ObjectDisposedException...), el estado final sigue siendo Cancelado.
                    // Antes cualquier excepción posterior pisaba el Cancelado y la
                    // cancelación aparecía como fallo (plan #1011).
                    if (planErr != null && planErr.Estado != "Cancelado")
                        await planRepoErr.UpdateEstadoAsync(idPlanLocal, "Fallido", DateTime.UtcNow, CancellationToken.None);
                } catch { }
            }
            finally
            {
                if (_activeExecutions.TryRemove(idPlanLocal, out var reg)) reg.Dispose();
            }
        });
    }

    /// <summary>
    /// Cancelación real (B-03): detiene el trabajo activo del plan invocando Cancel()
    /// y marca el estado. No se limita a cambiar el estado como antes.
    /// </summary>
    public async Task<bool> CancelarEjecucionAsync(int idPlan, CancellationToken ct = default)
    {
        // 1. Detener trabajo activo si existe. Se registra si se encontró CTS o
        // no: sin ese dato un {"cancelado":true} no prueba nada (plan #13237).
        if (_activeExecutions.TryGetValue(idPlan, out var cts))
        {
            _logger.LogInformation("Planner: cancelación con CTS activo para plan {IdPlan}.", idPlan);
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        else
        {
            _logger.LogWarning("Planner: cancelación sin CTS activo para plan {IdPlan}; solo se marca estado (el backstop por estado frenará las capas).", idPlan);
        }

        // 2. Marcar estado aunque ya no esté activo (encolado, en espera, etc.).
        await using var scope = _scopeFactory.CreateAsyncScope();
        var planRepo = scope.ServiceProvider.GetRequiredService<IPlanRepository>();
        var plan = await planRepo.GetByIdAsync(idPlan, ct);
        if (plan == null) return false;

        if (plan.Estado == "EnEjecucion" || plan.Estado == "IniciandoEjecucion" || plan.Estado == "EnEsperaAprobacion" || plan.Estado == "Borrador")
        {
            await planRepo.UpdateEstadoAsync(idPlan, "Cancelado", DateTime.UtcNow, ct);
        }
        await RegistrarLogAsync(idPlan, null, "PlanCancelado", "Cancelado por el usuario.", ct);
        return true;
    }

    /// <summary>
    /// Ejecuta un nodo del grafo con retry por nodo (ítem 6): ante Error, el supervisor
    /// decide reintento del MISMO nodo según política; la cancelación no se reintenta.
    /// </summary>
    /// <summary>
    /// Combina resultados en memoria de pasos anteriores (por Orden). Tope 12000
    /// caracteres por la cabeza (datos crudos primero). Null si no hay nada.
    /// </summary>
    private string? CombinarMemoria(int ordenTope)
    {
        var partes = _memoriaResultados
            .Where(kv => kv.Key < ordenTope)
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (partes.Count == 0) return null;
        var combinado = string.Join("\n", partes);
        const int max = 12000;
        return combinado.Length > max ? combinado[..max] + "\n… (datos truncados)" : combinado;
    }

    /// <summary>
    /// Contexto para pasos Agent: memoria en memoria primero (cola, lo más
    /// reciente = consolidado), relectura de BD como respaldo.
    /// </summary>
    private async Task<string> ConstruirContextoAgenteAsync(
        IPlanStepRepository stepRepo, Plan plan, PlanStep paso, CancellationToken ct)
    {
        var partes = _memoriaResultados
            .Where(kv => kv.Key < paso.Orden)
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (partes.Count > 0)
        {
            var combinado = string.Join("\n", partes);
            const int max = 6000;
            if (combinado.Length > max)
            {
                var cola = combinado[^max..];
                var corte = cola.IndexOf('\n');
                if (corte >= 0 && corte < cola.Length - 1) cola = cola[(corte + 1)..];
                combinado = "[contexto recortado: se conserva lo más reciente]\n" + cola;
            }
            return combinado;
        }
        var pasosAnteriores = (await stepRepo.GetByPlanAsync(plan.IdPlan, ct, true))
            .Where(p => p.Orden < paso.Orden && !string.IsNullOrWhiteSpace(p.Resultado))
            .Select(p => $"[Paso {p.Orden}: {p.Nombre}]\n{p.Resultado}");
        return string.Join("\n", pasosAnteriores);
    }

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
                    await PersistirPasoAsync(stepRepo, paso, "Cancelado", paso.Resultado, CancellationToken.None);
                }
                catch { }
                return;
            }
            if (!reintentar) return;
        }
    }

    /// <summary>
    /// Persiste el estado/resultado de un paso cargando la entidad FRESCA en el scope
    /// del repositorio. El `plan` del background es stale (trackeado por un DbContext
    /// ya dispuesto y con Pasos en Pendiente): actualizarlo directamente hacía
    /// graph-attach y SaveChanges revertía las filas Tool ya completadas.
    /// También sincroniza el objeto en memoria para el control del bucle de reintentos.
    /// </summary>
    private static async Task PersistirPasoAsync(
        IPlanStepRepository stepRepo, PlanStep pasoStale, string estado, string? resultado, CancellationToken ct)
    {
        pasoStale.Estado = estado;
        pasoStale.Resultado = resultado;
        var fresco = await stepRepo.GetByIdAsync(pasoStale.IdStep, ct);
        if (fresco != null)
        {
            fresco.Estado = estado;
            fresco.Resultado = resultado;
            await stepRepo.UpdateAsync(fresco, ct);
        }
        else
        {
            pasoStale.Plan = null; // romper el grafo: jamás adjuntar el plan stale
            await stepRepo.UpdateAsync(pasoStale, ct);
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
        // Con semáforo como los demás: sin él, este UpdateAsync corría en paralelo con
        // otros nodos sobre el mismo DbContext ("second operation started...").
        if (paso.Tipo == "Approval")
        {
            await _sem.WaitAsync(ct);
            try
            {
                var resApr = await _agentOrchestrator.EjecutarNodoValidadoAsync(
                    plan, paso, nodo, _huellaGrafoEjecucion, null, null, plan.IdUsuario, ct);
                if (resApr.Omitido)
                {
                    await PersistirPasoAsync(stepRepo, paso, "Omitido", paso.Resultado, ct);
                }
            }
            finally
            {
                _sem.Release();
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
                var resWf = await _agentOrchestrator.EjecutarNodoValidadoAsync(
                    plan, paso, nodo, _huellaGrafoEjecucion, null, null, plan.IdUsuario, ct);

                if (resWf.Exito)
                {
                    await PersistirPasoAsync(stepRepo, paso, "Completado", resWf.Resultado, ct);
                }
                else
                {
                    await PersistirPasoAsync(stepRepo, paso, "Error",
                        resWf.Error ?? "El workflow no devolvió resultado.", ct);
                }
            }
            catch (OperationCanceledException)
            {
                try
                {
                    await PersistirPasoAsync(stepRepo, paso, "Cancelado", "Ejecución cancelada o timeout", ct);
                }
                catch { }
            }
            catch (Exception ex)
            {
                try
                {
                    await PersistirPasoAsync(stepRepo, paso, "Error", $"Error: {ex.Message}", ct);
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

                // Inyectar resultados de pasos anteriores (memoria en vivo primero).
                var contextoPrevio = await ConstruirContextoAgenteAsync(stepRepo, plan, paso, ct);

                // Timeout más largo para CPU (15 min)
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

                // Ejecución vía Orchestrator sobre el NODO del DAG validado.
                var resAg = await _agentOrchestrator.EjecutarNodoValidadoAsync(
                    plan, paso, nodo, _huellaGrafoEjecucion, contextoPrevio, null, plan.IdUsuario, linkedCts.Token);

                if (resAg.Exito)
                {
                    await PersistirPasoAsync(stepRepo, paso, "Completado", resAg.Resultado, ct);
                    // Auditoría del paso Agent: sin este evento el paso no dejaba
                    // rastro (solo se auditaban Coordination y Tool/RAG), y en una
                    // aceptación no se podía evidenciar que el análisis, la
                    // clasificación de riesgos o la entrega se ejecutaron de verdad.
                    await RegistrarLogAsync(plan.IdPlan, paso.IdStep, "PasoAgentEjecutado",
                        $"'{paso.Nombre}' completado en {resAg.TiempoMs} ms.", ct);
                }
                else
                {
                    await PersistirPasoAsync(stepRepo, paso, "Error",
                        resAg.Error ?? "Sin respuesta del agente", ct);
                    await RegistrarLogAsync(plan.IdPlan, paso.IdStep, "PasoAgentError",
                        $"'{paso.Nombre}' falló: {resAg.Error ?? "Sin respuesta del agente"}", ct);
                }
            }
            catch (OperationCanceledException)
            {
                try {
                    await PersistirPasoAsync(stepRepo, paso, "Cancelado", "Ejecución cancelada o timeout", ct);
                } catch { }
                await RegistrarLogAsync(plan.IdPlan, paso.IdStep, "PasoAgentCancelado",
                    $"'{paso.Nombre}' cancelado o excedió el tiempo.", CancellationToken.None);
            }
            catch (Exception ex)
            {
                try {
                    await PersistirPasoAsync(stepRepo, paso, "Error", $"Error: {ex.Message}", ct);
                } catch { }
                await RegistrarLogAsync(plan.IdPlan, paso.IdStep, "PasoAgentError",
                    $"'{paso.Nombre}' falló: {ex.Message}", CancellationToken.None);
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
            TiempoEstimadoSegundos = tiempoEstimado,
            // El MISMO grafo que ejecuta LanzarEjecucionGrafo, construido con el mismo
            // ExecutionGraphBuilder sobre este plan. Se expone con su huella para que el
            // auditor compare la simulación contra el log de ejecución.
            Grafo = ConstruirGrafo(plan)
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

    /// <summary>
    /// Registra un evento de auditoría del plan.
    ///
    /// Usa un SCOPE PROPIO porque la ejecución corre en background (fire-and-forget):
    /// el endpoint devuelve el IdPlan de inmediato y el scope del request —del que
    /// viene el _logRepo del constructor— se dispone a los pocos milisegundos. Con
    /// ese repo, todo evento emitido tarde (pasos Agent, que tardan 80-150 s, o el
    /// retry) moría con ObjectDisposedException, y esa excepción al caer en el catch
    /// genérico convertía una cancelación limpia en plan "Fallido" (plan #1011).
    /// Además, la auditoría quedaba incompleta justo en los pasos largos.
    /// </summary>
    public async Task RegistrarLogAsync(int idPlan, int? idStep, string evento, string? detalle, CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var logRepo = scope.ServiceProvider.GetRequiredService<IPlanExecutionLogRepository>();
            await logRepo.AddAsync(new PlanExecutionLog
            {
                IdPlan = idPlan,
                IdStep = idStep,
                Evento = evento,
                Detalle = detalle,
                Fecha = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe tumbar la ejecución del plan.
            _logger.LogWarning(ex, "Planner: no se pudo registrar el evento '{Evento}' del plan {IdPlan}.", evento, idPlan);
        }
    }
}
