using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Application.Aprobaciones;
using Asistente.Domain.Entities;
using Asistente.Domain.Entities.Aprobaciones;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Planner Engine (ETAPA 18). Orquesta Plan Builder, Plan Validator, Execution Graph Builder
/// y Execution Supervisor. Convierte lenguaje natural en un plan ejecutable y lo delega al
/// Agent Orchestrator (ETAPA 17) — NUNCA ejecuta acciones directamente (Regla 3).
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
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IAsistenteRepository _asistenteRepo;
    private readonly IAgentExecutionRepository _execRepo;
    private readonly IAgentExecutionStepRepository _execStepRepo;
    private readonly ILogger<PlannerEngine> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ApprovalManager _approvalManager;
    private readonly IToolOrchestrator _toolOrchestrator;

    public PlannerEngine(
        PlanBuilder builder,
        PlanValidator validator,
        ExecutionGraphBuilder graphBuilder,
        ExecutionSupervisor supervisor,
        IPlanRepository planRepo,
        IPlanStepRepository stepRepo,
        IPlanDependencyRepository depRepo,
        IPlanExecutionLogRepository logRepo,
        IAgentOrchestrator orchestrator,
        IAsistenteRepository asistenteRepo,
        IAgentExecutionRepository execRepo,
        IAgentExecutionStepRepository execStepRepo,
        ILogger<PlannerEngine> logger,
        IServiceScopeFactory scopeFactory,
        ApprovalManager approvalManager,
        IToolOrchestrator toolOrchestrator)
    {
        _builder = builder;
        _validator = validator;
        _graphBuilder = graphBuilder;
        _supervisor = supervisor;
        _planRepo = planRepo;
        _stepRepo = stepRepo;
        _depRepo = depRepo;
        _logRepo = logRepo;
        _orchestrator = orchestrator;
        _asistenteRepo = asistenteRepo;
        _execRepo = execRepo;
        _execStepRepo = execStepRepo;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _approvalManager = approvalManager;
        _toolOrchestrator = toolOrchestrator;
    }

    public async Task<Plan> GenerarPlanAsync(string objetivo, int idUsuario, CancellationToken ct = default)
    {
        var plan = await _builder.ConstruirAsync(objetivo, idUsuario, ct);

        // Persistencia en UNA sola unidad: el Plan ya trae Pasos y Dependencias como
        // propiedades de navegación, así que un solo SaveChanges resuelve las FKs hijas.
        // NO se debe re-hacer AddAsync sobre los pasos (ya están trackeados por el contexto
        // al guardar el plan) — eso dispara "cannot be tracked" / FK violation.
        plan = await _planRepo.AddAsync(plan, ct);

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

        // Guarda anti-doble-ejecución (Bug #1006): si el plan ya está EnEjecucion, no volver a
        // dispararlo. Evita dos backgrounds concurrentes que compiten por el mismo AgentExecution
        // y producen carreras de DbContext ("A second operation was started on this context...").
        if (plan.Estado == "EnEjecucion")
            throw new InvalidOperationException($"El plan {idPlan} ya está en ejecución.");

        var validacion = await ValidarPlanAsync(plan, ct);
        if (!validacion.Valido)
            throw new InvalidOperationException("El plan no pasó la validación: " + string.Join("; ", validacion.Errores));

        // Aprobación Human-in-the-Loop (ETAPA 19): si el plan tiene pasos de tipo Approval,
        // se CREA la solicitud, se PAUSA el plan y se ESPERA la decisión del aprobador.
        // Solo si se aprueba se delega al Orchestrator. Si se rechaza, el ApprovalManager
        // ya marcó el plan como Cancelado y no se ejecuta.
        var pasosAprobacion = plan.Pasos.Where(p => p.Tipo == "Approval").ToList();
        if (pasosAprobacion.Any())
        {
            plan.RequiereAprobacion = true;
            foreach (var paso in pasosAprobacion)
            {
                // Aprobadores: el supervisor del agente principal o, por defecto, el usuario admin (1).
                var aprobadores = new List<int> { 1 }; // admin como aprobador principal por defecto
                var solicitud = await _approvalManager.CrearSolicitudAsync(
                    plan.IdPlan,
                    TipoAprobacion.Manual,
                    plan.IdUsuario,
                    $"Paso requerido: {paso.Nombre}",
                    aprobadores,
                    ct: ct);

                // Pausar el plan mientras se resuelve la aprobación.
                plan.Estado = "EnEsperaAprobacion";
                await _planRepo.UpdateAsync(plan, ct);
                await RegistrarLogAsync(plan.IdPlan, paso.Orden, "PlanPausadoAprobacion",
                    $"Plan pausado. Esperando aprobación {solicitud.Codigo}.", ct);

                // Esperar la resolución (timeout generoso: 1h por defecto).
                var resuelta = await _approvalManager.EsperarResolucionAsync(
                    solicitud.IdApproval, TimeSpan.FromHours(1), ct);

                if (resuelta.Estado != EstadoAprobacion.Aprobado)
                {
                    // Rechazada / expirada / cancelada: el ApprovalManager ya gestionó el plan.
                    await RegistrarLogAsync(plan.IdPlan, paso.Orden, "PlanNoAprobado",
                        $"Solicitud {solicitud.Codigo} resolvió como {resuelta.Estado}. Plan no ejecutado.", ct);
                    return new AgentExecutionResult
                    {
                        Estado = "Cancelado",
                        Exitoso = false,
                        Error = $"Plan no aprobado: {resuelta.Estado}"
                    };
                }
            }
        }
        else if (plan.RequiereAprobacion && !plan.Aprobado)
        {
            throw new InvalidOperationException("El plan requiere aprobación humana antes de ejecutarse.");
        }

        // ETAPA 19.1: ejecución REAL de pasos Tool (SqlQueryTool) antes de delegar al Orchestrator.
        // El Orchestrator solo pasa el texto al LLM y nunca invoca SqlQueryTool, por lo que los
        // pasos de datos quedaban como "no tengo acceso". Aquí resolvemos los pasos tipo "Tool"
        // ejecutando la herramienta de verdad y guardamos el resultado en PlanStep.Resultado.
        // Nota: plan.Pasos puede no venir cargado desde GetByIdAsync, así que recargamos explícitos.
        var pasosTool = (await _stepRepo.GetByPlanAsync(plan.IdPlan, ct))
            .Where(p => p.Tipo == "Tool" || p.Tipo == "Coordination").ToList();
        // ETAPA 19.3: el paso 0 (Coordination) lo resuelve el Planner directamente (sin LLM),
        // generando un texto de coordinación que describe el plan y los delegados.
        // Se guarda en un SCOPE PROPIO para que sea visible inmediatamente al background task
        // (evita race condition donde SincronizarPlanStepsAsync lo sobrescribe).
        var pasoCoordinacion = pasosTool.FirstOrDefault(p => p.Tipo == "Coordination");
        if (pasoCoordinacion != null)
        {
            var textoCoordinacion = GenerarTextoCoordinacion(plan);
            await using var scopeCoord = _scopeFactory.CreateAsyncScope();
            var stepRepoCoord = scopeCoord.ServiceProvider.GetRequiredService<IPlanStepRepository>();
            var pasoCoordDb = await stepRepoCoord.GetByIdAsync(pasoCoordinacion.IdStep, ct);
            if (pasoCoordDb != null)
            {
                pasoCoordDb.Resultado = textoCoordinacion;
                pasoCoordDb.Estado = "Completado";
                await stepRepoCoord.UpdateAsync(pasoCoordDb, ct);
            }
            await RegistrarLogAsync(plan.IdPlan, pasoCoordinacion.Orden, "PasoCoordinacion",
                "Plan de acción generado por el coordinador.", ct);
        }

        // ETAPA 19.2: guardamos el resultado del paso anterior para pasarlo al ReportTool como 'datos'.
        string? resultadoAnterior = null;
        foreach (var paso in pasosTool)
        {
            try
            {
                var herramienta = paso.CodigoHerramienta ?? "SqlQueryTool";
                var parametros = new Dictionary<string, object?>();
                if (herramienta == "ReportTool")
                {
                    parametros["titulo"] = paso.Nombre;
                    parametros["datos"] = resultadoAnterior ?? plan.Objetivo;
                }
                else
                {
                    // ETAPA 19.2: usar el NOMBRE del paso como pregunta para detectar la intención
                    // (SELECT * vs GROUP BY), y el objetivo para detectar la tabla.
                    // "Consultar datos" → SELECT * | "Analizar indicadores" → GROUP BY
                    parametros["pregunta"] = plan.Objetivo;
                    var nombreLower = paso.Nombre.ToLowerInvariant();
                    var quiereAgregacion = nombreLower.Contains("indicador") || nombreLower.Contains("calcular") ||
                        nombreLower.Contains("métrica") || nombreLower.Contains("metrica") ||
                        nombreLower.Contains("agrupar") || nombreLower.Contains("agrupado") ||
                        nombreLower.Contains("conteos") || nombreLower.Contains("totales");
                    parametros["forzarAgregacion"] = quiereAgregacion;
                    // forzarRaw: el paso "Consultar datos" debe mostrar filas raw (no GROUP BY),
                    // aunque el objetivo contenga palabras de agregación (ej. "resumen").
                    var forzarRaw = nombreLower.Contains("consultar") || nombreLower.Contains("obtener") ||
                        nombreLower.Contains("listar") || nombreLower.Contains("mostrar") ||
                        nombreLower.Contains("detalle") || nombreLower.Contains("datos");
                    parametros["forzarRaw"] = forzarRaw;
                }

                var resTool = await _toolOrchestrator.EjecutarAsync(new ToolExecutionRequest
                {
                    HerramientaCodigo = herramienta,
                    Parametros = parametros,
                    IdUsuario = plan.IdUsuario,
                    IdAsistente = paso.IdAsistente,
                    PreguntaOriginal = paso.Nombre + " " + plan.Objetivo
                }, ct);

                if (resTool.Exitoso)
                {
                    paso.Resultado = resTool.Contenido;
                    paso.Estado = "Completado";
                    await _stepRepo.UpdateAsync(paso, ct);
                    resultadoAnterior = resTool.Contenido;
                    await RegistrarLogAsync(plan.IdPlan, paso.Orden, "PasoToolEjecutado",
                        $"'{herramienta}' ejecutó '{paso.Nombre}' con éxito.", ct);
                }
                else
                {
                    paso.Resultado = $"{herramienta}: " + resTool.Error;
                    await _stepRepo.UpdateAsync(paso, ct);
                    await RegistrarLogAsync(plan.IdPlan, paso.Orden, "PasoToolSinDatos",
                        $"{herramienta}: {resTool.Error}", ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Planner: fallo al ejecutar '{Herramienta}' para el paso {Paso}", paso.CodigoHerramienta, paso.Nombre);
                paso.Resultado = $"Error al ejecutar {paso.CodigoHerramienta}: " + ex.Message;
                await _stepRepo.UpdateAsync(paso, ct);
            }
        }

        // Regla 4: la ejecución pasa SIEMPRE por el Orchestrator.
        await _supervisor.IniciarAsync(plan, ct);

        var principal = plan.Pasos.FirstOrDefault(p => p.Tipo == "Agent") ?? plan.Pasos.First();
        var request = new AgentRequest
        {
            IdUsuario = plan.IdUsuario,
            IdAgentePrincipal = principal.IdAsistente ?? 1008,
            Pregunta = plan.Objetivo,
            PermitirColaboracion = true,
            // ETAPA 19.3: pasar resultados de los pasos Tool como contexto para que
            // los pasos Agent tengan datos reales y no inventen valores.
            ContextoPrevio = string.Join("\n", pasosTool
                .Where(p => p.Tipo == "Tool" && !string.IsNullOrWhiteSpace(p.Resultado))
                .Select(p => $"[Paso {p.Orden}: {p.Nombre}]\n{p.Resultado}"))
        };

        // Crear la ejecución YA para obtener el IdExecution de inmediato (no esperar el grafo,
        // que en CPU tarda minutos). Así el Planner muestra el IdExecution al segundo 1 y el
        // usuario puede enlazar a Trazas del Orchestrator desde el inicio (RF §19 punto 5).
        var idExecution = await _orchestrator.IniciarAsync(request, ct);
        plan.IdExecution = idExecution.ToString();
        plan.Estado = "EnEjecucion";
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarLogAsync(plan.IdPlan, null, "PlanEjecutado",
            $"Plan delegado al Agent Orchestrator. IdExecution={idExecution}.", ct);

        // Grafo en segundo plano (fire-and-forget): NO bloquea la respuesta del Planner.
        // IMPORTANTE: el trabajo en background debe correr en su PROPIO scope (su propio
        // DbContext). Los servicios del scope de la request HTTP se disponen al retornar la
        // respuesta; usarlos en un Task.Run disparado causaba ObjectDisposedException silenciosa
        // (el plan quedaba EnEjecucion para siempre y nunca se reintentaba). Por eso resolvemos
        // un PlannerEngine fresco desde un scope nuevo dentro del propio background.
        var idPlanLocal = plan.IdPlan;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var engine = scope.ServiceProvider.GetRequiredService<IPlannerEngine>();
                await engine.EjecutarGrafoConReintentosAsync(idPlanLocal, idExecution, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error irrecuperable en background del plan {IdPlan}", idPlanLocal);
            }
        });

        return new AgentExecutionResult
        {
            IdExecution = idExecution,
            Estado = "EnEjecucion",
            Exitoso = false
        };
    }

    /// <summary>Ejecuta el grafo del Orchestrator DENTRO de un scope propio (su propio DbContext)
    /// y devuelve el estado final de la ejecución ("Completado"/"Error"). El scope se mantiene
    /// VIVO hasta que el grafo termina: el grafo ya aplica su propio timeout por nodo
    /// (ConfiguracionOrchestrator.MaxTiempoTotalMs), por lo que NO debemos cortarlo nosotros.
    /// Un tope de seguridad muy amplio (2h) solo protege contra un cuelgue absoluto del grafo
    /// sin matar el DbContext del scope (en ese caso cancelamos y dejamos que el grafo termine).</summary>
    private async Task<string> EjecutarGrafoEnScopeAsync(int idExecution, AgentRequest request)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
        var execRepo = scope.ServiceProvider.GetRequiredService<IAgentExecutionRepository>();

        using var ctsSeguridad = new CancellationTokenSource(TimeSpan.FromHours(2));
        var grafoTask = orchestrator.EjecutarGrafoAsync(idExecution, request, ctsSeguridad.Token);
        // Esperamos SIEMPRE a que el grafo finalice (Completado/Error); el scope sobrevive.
        await grafoTask;

        var execResult = await execRepo.GetByIdAsync(idExecution, CancellationToken.None);
        return execResult?.Estado ?? "Error";
    }

    public async Task EjecutarGrafoConReintentosAsync(int idPlan, int idExecution, AgentRequest request)
    {
        var plan = await _planRepo.GetByIdAsync(idPlan, CancellationToken.None)
                   ?? throw new InvalidOperationException($"Plan {idPlan} no encontrado en background.");

        bool exito = false;
        Exception? ultimoError = null;
        for (int intento = 1; intento <= _supervisor.MaxReintentos + 1; intento++)
        {
            try
            {
                // Cada intento corre en su PROPIO scope (su propio DbContext). Así, si un intento
                // anterior sigue vivo cuando arranca el siguiente (el grafo no se cancela al vencer
                // el WhenAny), NO comparten el DbContext y no hay carrera
                // ("A second operation was started on this context instance").
                var estadoGrafo = await EjecutarGrafoEnScopeAsync(idExecution, request);

                // El Orchestrator finaliza como Completado o Error; el éxito se determina por estado.
                if (estadoGrafo != "Completado")
                    throw new Exception($"La ejecución del Orchestrator falló (estado: {estadoGrafo}).");

                exito = true;
                break;
            }
            catch (Exception ex)
            {
                ultimoError = ex;
                if (intento <= _supervisor.MaxReintentos)
                {
                    await _supervisor.RegistrarReintentoPlanAsync(plan, intento, ex.Message, CancellationToken.None);
                    await Task.Delay(_supervisor.IntervaloMs, CancellationToken.None);
                }
            }
        }

        // Reflejo final (estados definitivos).
        await SincronizarPlanStepsAsync(plan.IdPlan, idExecution.ToString(), CancellationToken.None);

        var exec = await _execRepo.GetByIdAsync(idExecution, CancellationToken.None);
        plan.Estado = exito && exec?.Estado == "Completado" ? "Completado"
                    : (!exito ? "Fallido" : plan.Estado);
        plan.FechaFin = exec?.FechaFin ?? DateTime.UtcNow;
        plan.TiempoTotalMs = exec?.TiempoTotalMs;
        await _planRepo.UpdateAsync(plan, CancellationToken.None);
        await RegistrarLogAsync(plan.IdPlan, null, exito ? "PlanFinalizado" : "PlanError",
            exito
                ? $"Plan finalizado con estado {plan.Estado}. IdExecution={idExecution}."
                : $"Plan falló tras {_supervisor.MaxReintentos} reintentos: {ultimoError?.Message}",
            CancellationToken.None);
    }

    /// <summary>Sincroniza el estado de los PlanStep con los AgentExecutionStep del Orchestrator
    /// (el Orchestrator escribe el progreso fino en su propia tabla; el Planner lo refleja para
    /// que el DAG se vea en vivo). Mapea por Orden del paso. Cuando la ejecución del Orchestrator
    /// alcanza un estado terminal, propaga ese resultado a TODOS los pasos del plan (el plan es
    /// una abstracción de 7 pasos que se delega a un grafo de 3 nodos; no mapean 1:1).</summary>
    public async Task SincronizarPlanStepsAsync(int idPlan, string idExecution, CancellationToken ct = default)
    {
        if (!int.TryParse(idExecution, out var idExec)) return;
        var planSteps = await _stepRepo.GetByPlanAsync(idPlan, ct);
        if (planSteps.Count == 0) return;
        var execSteps = await _execStepRepo.GetByExecutionAsync(idExec, ct);
        var exec = await _execRepo.GetByIdAsync(idExec, ct);

        // Mapeo fino por Orden (progreso en vivo de los nodos que el Orchestrator ejecuta).
        // NOTA: los pasos "Coordination" (paso 0) los maneja el Planner directamente, no el Orchestrator.
        // Se excluyen completamente para que SincronizarPlanStepsAsync NO sobreescriba su resultado.
        foreach (var ps in planSteps.Where(p => p.Tipo != "Coordination"))
        {
            var es = execSteps.FirstOrDefault(e => e.Orden == ps.Orden);
            if (es == null) continue;
            var nuevo = es.Estado switch
            {
                "Completado" => "Completado",
                "Error" => "Error",
                "EnEjecucion" => "EnEjecucion",
                _ => ps.Estado
            };
            if (ps.Estado != nuevo)
            {
                ps.Estado = nuevo;
                if (!string.IsNullOrWhiteSpace(es.Resultado)) ps.Resultado = es.Resultado;
                await _stepRepo.UpdateAsync(ps, ct);
            }
        }

        // Propagación terminal: si el Orchestrator terminó, el plan completo refleja ese resultado.
        // NOTA: los pasos "Coordination" (paso 0) los maneja el Planner directamente y tienen resultado
        // generado por GenerarTextoCoordinacion. Se excluyen para NO sobreescribir con la respuesta del Orchestrator.
        if (exec?.Estado == "Completado")
        {
            foreach (var ps in planSteps.Where(p => p.Estado != "Completado" && p.Tipo != "Coordination"))
            {
                var es = execSteps.FirstOrDefault(e => e.Orden == ps.Orden);
                ps.Estado = "Completado";
                if (es != null && !string.IsNullOrWhiteSpace(es.Resultado))
                    ps.Resultado = es.Resultado;
                else if (!string.IsNullOrWhiteSpace(exec.RespuestaFinal))
                    ps.Resultado = exec.RespuestaFinal;
                await _stepRepo.UpdateAsync(ps, ct);
            }

            // ETAPA 19.3: re-aplicar el texto de coordinación al final para garantizar que nunca se pierda
            // (el Orchestrator o la sincronización pueden haberlo sobreescrito).
            var coordStep = planSteps.FirstOrDefault(p => p.Tipo == "Coordination");
            if (coordStep != null)
            {
                var plan = await _planRepo.GetByIdAsync(idPlan, ct);
                coordStep.Resultado = GenerarTextoCoordinacion(plan!);
                coordStep.Estado = "Completado";
                await _stepRepo.UpdateAsync(coordStep, ct);
            }
        }
        else if (exec?.Estado == "Error")
        {
            foreach (var ps in planSteps.Where(p => p.Estado is "Pendiente" or "EnEjecucion"))
            {
                ps.Estado = "Error";
                await _stepRepo.UpdateAsync(ps, ct);
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
        // Se resuelve el NOMBRE del agente por IdAsistente (PlanStep.Nombre guarda la
        // descripción del paso, no el agente). Si no se encuentra, se muestra el código/id.
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
    /// ETAPA 19.3: genera un texto de coordinación que describe el plan de acción y los delegados.
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
