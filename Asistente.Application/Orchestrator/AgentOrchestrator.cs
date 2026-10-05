using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Application.Services.Herramientas;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Orchestrator;

/// <summary>
/// Implementación del Agent Orchestrator (RF Actividades 1, 5, 6, 8, 9, 10).
/// Coordina la colaboración entre agentes a través de un Execution Graph:
///  - Nunca un agente invoca directamente a otro (Regla 1): el Orchestrator es el único que
///    ejecuta los agentes, pasando por el Agent Runtime (IChatService).
///  - Control de profundidad, máx agentes y tiempo (Actividad 8).
///  - Manejo de errores con estrategia configurable (Actividad 9).
///  - Trazabilidad completa en AgentExecution / AgentExecutionStep / AgentExecutionTrace (Regla 6).
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IAgentSelector _selector;
    private readonly IResponseAggregator _aggregator;
    private readonly IContextManager _contextManager;
    private readonly IAgentExecutionRepository _execRepo;
    private readonly IAgentExecutionStepRepository _stepRepo;
    private readonly IAgentExecutionTraceRepository _traceRepo;
    private readonly IAgentCollaborationRuleRepository _reglasRepo;
    private readonly IConfiguracionOrchestratorRepository _configRepo;
    private readonly IAsistenteRepository _asistenteRepo;
    private readonly IChatService _chatService;
    private readonly IOllamaService _ollama;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IAgentSelector selector,
        IResponseAggregator aggregator,
        IContextManager contextManager,
        IAgentExecutionRepository execRepo,
        IAgentExecutionStepRepository stepRepo,
        IAgentExecutionTraceRepository traceRepo,
        IAgentCollaborationRuleRepository reglasRepo,
        IConfiguracionOrchestratorRepository configRepo,
        IAsistenteRepository asistenteRepo,
        IChatService chatService,
        IOllamaService ollama,
        IServiceScopeFactory scopeFactory,
        ILogger<AgentOrchestrator> logger)
    {
        _selector = selector;
        _aggregator = aggregator;
        _contextManager = contextManager;
        _execRepo = execRepo;
        _stepRepo = stepRepo;
        _traceRepo = traceRepo;
        _reglasRepo = reglasRepo;
        _configRepo = configRepo;
        _asistenteRepo = asistenteRepo;
        _chatService = chatService;
        _ollama = ollama;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Ejecuta la solicitud completa de forma síncrona (usado por ChatService en modo
    /// híbrido). Crea la ejecución y corre el grafo, esperando el resultado.</summary>
    public async Task<AgentExecutionResult> ExecuteAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        var id = await IniciarAsync(request, cancellationToken);
        await EjecutarGrafoAsync(id, request, cancellationToken);
        var exec = await _execRepo.GetByIdAsync(id, cancellationToken);
        return new AgentExecutionResult
        {
            IdExecution = id,
            Exitoso = exec?.Estado == "Completado",
            RespuestaFinal = exec?.RespuestaFinal,
            Estado = exec?.Estado ?? "EnProceso"
        };
    }

    /// <summary>Crea la ejecución (EnProceso) y devuelve su IdExecution de inmediato, sin esperar
    /// a que termine el grafo. Permite que la API responda al instante y el Web redirija a Trazas.
    /// El grafo se ejecuta en segundo plano vía EjecutarGrafoAsync (fire-and-forget).</summary>
    public async Task<int> IniciarAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        var principal = await _asistenteRepo.GetByIdAsync(request.IdAgentePrincipal);
        var execution = new AgentExecution
        {
            IdUsuario = request.IdUsuario,
            IdAgentePrincipal = request.IdAgentePrincipal,
            Pregunta = request.Pregunta,
            FechaInicio = DateTime.UtcNow,
            Estado = "EnProceso"
        };
        execution = await _execRepo.AddAsync(execution, cancellationToken);
        await RegistrarTrazaAsync(execution.IdExecution, "Inicio",
            $"Agente principal: {principal?.Nombre} (Id {principal?.IdAsistente}). Pregunta: {request.Pregunta}", cancellationToken);
        return execution.IdExecution;
    }

    /// <summary>Ejecuta el Execution Graph completo (selección, construcción, ejecución por capas y
    /// consolidación) para una ejecución ya creada. Diseñado para correr en segundo plano.</summary>
    public async Task EjecutarGrafoAsync(int idExecution, AgentRequest request, CancellationToken cancellationToken = default)
    {
        var config = await _configRepo.GetAsync();
        var principal = await _asistenteRepo.GetByIdAsync(request.IdAgentePrincipal);
        // Cargar la ejecución YA creada por IniciarAsync (conserva IdAgentePrincipal y FKs).
        // NO crear un AgentExecution nuevo con IdAgentePrincipal=0: al hacer UpdateAsync en
        // FinalizarAsync se pisaría la FK y lanzaría DbUpdateException (Error 547).
        var execution = await _execRepo.GetByIdAsync(idExecution, cancellationToken)
                        ?? new AgentExecution { IdExecution = idExecution, IdUsuario = request.IdUsuario, Estado = "EnProceso" };
        var result = new AgentExecutionResult { IdExecution = idExecution };

        try
        {
            // Pre-flight: si Ollama (el LLM) no está disponible, el grafo completo fallaría
            // nodo por nodo y se colgaría. Fallamos de inmediato para que el Planner aplique
            // su política de reintentos en segundos, no tras minutos.
            if (!await _ollama.IsDisponibleAsync(cancellationToken))
            {
                await FinalizarAsync(execution, result, false,
                    "Ollama (LLM) no disponible. Verifique que el servicio de modelos esté ejecutándose.", cancellationToken);
                return;
            }

            // 1) Selección de agentes colaboradores (Actividad 2)
            var candidatos = (await _selector.SelectAgentsAsync(request, cancellationToken)).ToList();
            await RegistrarTrazaAsync(execution.IdExecution, "SeleccionAgentes",
                "Candidatos seleccionados: " + string.Join(", ", candidatos.Select(c => c.Nombre)), cancellationToken);

            // 2) Construcción del Execution Graph (mejora arquitectónica del RF)
            var grafo = ConstruirGrafo(execution.IdExecution, principal!, candidatos, request, config);

            // 3) Control de profundidad y máx agentes (Actividad 8)
            var capas = grafo.ObtenerCapas();
            var profundidad = grafo.ProfundidadMaxima();
            if (profundidad > (config?.MaxProfundidad ?? 3))
            {
                await FinalizarAsync(execution, result, false,
                    $"Se excedió la profundidad máxima ({profundidad} > {config?.MaxProfundidad}).", cancellationToken);
                return;
            }

            // Contexto global compartido (Actividad 4)
            var contextoGlobal = new SharedContext
            {
                IdUsuario = request.IdUsuario,
                PreguntaOriginal = request.Pregunta
            };

            // ETAPA 19.3: inyectar resultados de pasos Tool anteriores como contexto
            // para que los agentes tengan datos reales y no inventen valores.
            if (!string.IsNullOrWhiteSpace(request.ContextoPrevio))
            {
                contextoGlobal.ResultadosPrevios.Add(new ContextoParcial
                {
                    IdAgente = 0,
                    NombreAgente = "Planner Engine",
                    Contenido = request.ContextoPrevio
                });
            }

            // 4) Ejecución por capas: PARALELO dentro de capa (B-02), secuencial entre
            // capas (Actividades 5 y 6). Cada nodo corre en su propio scope (aislamiento
            // de DbContext por rama); lo compartido se consolida después en orden.
            var resultadosParciales = new List<ContextoParcial>();
            var ordenWrap = new int[1]; // contador thread-safe para orden de pasos

            foreach (var capa in capas)
            {
                var pendientes = new List<ExecutionNode>();
                foreach (var nodo in capa)
                {
                    // ETAPA 19: los nodos de aprobación (Human-in-the-Loop) NO se ejecutan aquí;
                    // los gestiona el ApprovalManager a nivel de Planner. Se marcan Omitido para
                    // que el grafo conserve la trazabilidad sin invocar al agente.
                    if (nodo.EsAprobacion)
                    {
                        nodo.Estado = "Omitido";
                        await RegistrarTrazaAsync(execution.IdExecution, "AprobacionOmitida",
                            $"Paso de aprobación '{nodo.Accion}' omitido en el grafo (gestionado por ApprovalManager).", cancellationToken);
                        continue;
                    }

                    pendientes.Add(nodo);
                }

                await Task.WhenAll(pendientes.Select(async nodo =>
                {
                    await using var scopeNodo = _scopeFactory.CreateAsyncScope();
                    var orchNodo = scopeNodo.ServiceProvider.GetRequiredService<IAgentOrchestrator>();
                    // ETAPA 19.3: inyectar contexto completo de pasos anteriores para que
                    // el LLM del agente tenga datos reales y no invente valores.
                    await orchNodo.EjecutarNodoAisladoAsync(
                        execution, nodo, contextoGlobal, request.ContextoPrevio,
                        ordenWrap, config, cancellationToken);
                }));

                // Consolidación en orden determinista (IdNodo), tras la capa.
                foreach (var nodo in pendientes.OrderBy(n => n.IdNodo))
                {
                    if (nodo.Estado == "Completado" && !string.IsNullOrWhiteSpace(nodo.Resultado))
                    {
                        contextoGlobal.ResultadosPrevios.Add(new ContextoParcial
                        {
                            IdAgente = nodo.IdAgente,
                            NombreAgente = nodo.NombreAgente,
                            Contenido = nodo.Resultado
                        });
                        resultadosParciales.Add(contextoGlobal.ResultadosPrevios.Last());
                    }
                }
            }

            // 5) Consolidación (Actividad 7 / Regla 7)
            execution = await _execRepo.GetByIdAsync(execution.IdExecution, cancellationToken) ?? execution;
            execution.CantidadAgentes = grafo.Nodos.Count;
            execution.ProfundidadAlcanzada = profundidad;
            execution.HerramientasUtilizadas = grafo.Nodos.Count; // aproximación: 1 herramienta clave por agente

            var respuesta = await _aggregator.BuildFinalResponseAsync(execution, cancellationToken);
            await RegistrarTrazaAsync(execution.IdExecution, "Consolidacion",
                $"Respuesta consolidada ({respuesta.Length} caracteres).", cancellationToken);

            execution.RespuestaFinal = respuesta;
            await FinalizarAsync(execution, result, true, null, cancellationToken);
            result.RespuestaFinal = respuesta;
            result.AgentesParticipantes = grafo.Nodos.Select(n => n.NombreAgente).Distinct().ToList();
            result.Pasos = grafo.Nodos.OrderBy(n => n.IdNodo).Select(n => new AgentStepResult
            {
                Orden = n.IdNodo,
                IdAgente = n.IdAgente,
                NombreAgente = n.NombreAgente,
                Accion = n.Accion,
                Resultado = n.Resultado,
                TiempoMs = n.TiempoMs,
                Estado = n.Estado,
                Dependencias = n.DependeDe
            }).ToList();
            result.Trazas = (await _traceRepo.GetByExecutionAsync(execution.IdExecution, cancellationToken))
                .Select(t => new ExecutionTraceDto { Evento = t.Evento, Detalle = t.Detalle, FechaHora = t.FechaHora }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Agent Orchestrator (Execution {Id})", execution.IdExecution);
            await FinalizarAsync(execution, result, false, $"Error interno: {ex.Message}", cancellationToken);
        }
    }

    /// <summary>
    /// Ejecuta UN paso de un plan validado de Etapa 18. Fuente de verdad: el PlanStep
    /// recibido (nodos, orden y dependencias los gobierna el Planner). El Orchestrator
    /// NO re-selecciona agentes ni reconstruye el grafo: solo ejecuta el paso.
    /// </summary>
    public async Task<ResultadoPasoOrquestado> EjecutarPasoValidadoAsync(
        Plan plan, PlanStep paso, string? contextoPrevio, string? datoPrevio,
        int idUsuario, CancellationToken cancellationToken = default)
    {
        var inicio = DateTime.UtcNow;
        long Transcurrido() => (long)(DateTime.UtcNow - inicio).TotalMilliseconds;

        // Scope propio: el Planner ejecuta en background (fire-and-forget) y el scope
        // del request ya puede estar dispuesto. Resolver servicios frescos por llamada.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var toolOrchestrator = sp.GetRequiredService<IToolOrchestrator>();
        var chatService = sp.GetRequiredService<IChatService>();
        var workflowEngine = sp.GetRequiredService<IWorkflowEngine>();
        var configRepo = sp.GetRequiredService<IConfiguracionOrchestratorRepository>();

        try
        {
            // Coordination: texto fijo, sin LLM (igual que PlannerEngine).
            if (paso.Tipo == "Coordination")
            {
                return new ResultadoPasoOrquestado
                {
                    Exito = true,
                    Resultado = GenerarTextoCoordinacion(plan),
                    TiempoMs = Transcurrido()
                };
            }

            // Approval: lo gestiona ApprovalManager a nivel Planner; aquí se omite.
            if (paso.Tipo == "Approval")
            {
                return new ResultadoPasoOrquestado { Exito = true, Omitido = true, TiempoMs = Transcurrido() };
            }

            // Workflow: motor de workflows con el IdWorkflow del paso.
            if (paso.Tipo == "Workflow")
            {
                if (!paso.IdWorkflow.HasValue)
                    return new ResultadoPasoOrquestado { Exito = false, Error = "Paso Workflow sin IdWorkflow.", TiempoMs = Transcurrido() };

                // Contexto inicial desde el contrato máquina-máquina del plan: si
                // un paso RAG hermano lleva la preferencia {"documento":"..."},
                // viaja como {{Nombre}} para que las plantillas del workflow no
                // queden literales ("# Resumen: {{Nombre}}", plan #14251). Sin
                // paso RAG con preferencia, no se inventa nada.
                Dictionary<string, string>? contextoInicial = null;
                var docHermano = plan.Pasos
                    .Where(p => p.Tipo == "RAG" && !string.IsNullOrWhiteSpace(p.Entrada))
                    .Select(p => ExtraerDocumentoDeEntrada(p.Entrada))
                    .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
                if (!string.IsNullOrWhiteSpace(docHermano))
                    contextoInicial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Nombre"] = docHermano,
                        ["Documento"] = docHermano
                    };

                var wf = await workflowEngine.EjecutarAsync(
                    paso.IdWorkflow.Value, idUsuario,
                    paso.IdAsistente > 0 ? paso.IdAsistente : null,
                    confirmado: true, cancellationToken: cancellationToken,
                    contextoInicial: contextoInicial);

                if (wf != null && wf.Exitoso && !wf.RequiereConfirmacion)
                    return new ResultadoPasoOrquestado { Exito = true, Resultado = wf.ResultadoFinal, TiempoMs = Transcurrido() };
                return new ResultadoPasoOrquestado
                {
                    Exito = false,
                    Error = wf?.ResultadoFinal ?? "El workflow no devolvió resultado.",
                    TiempoMs = Transcurrido()
                };
            }

            // Tool / RAG: ejecución vía ToolOrchestrator (autorización + auditoría + timeout).
            if (paso.Tipo == "Tool" || paso.Tipo == "RAG")
            {
                var herramienta = paso.CodigoHerramienta
                    ?? (paso.Tipo == "RAG" ? "DocumentSearchTool" : "SqlQueryTool");
                var parametros = new Dictionary<string, object?>();
                // Pregunta efectiva del paso (para PreguntaOriginal): por defecto el
                // objetivo; la rama SqlQueryTool la puede refinar desde su Entrada.
                var preguntaPaso = plan.Objetivo;
                if (herramienta == "ReportTool")
                {
                    parametros["titulo"] = paso.Nombre;
                    // Handoff en memoria (datos ya combinados por el Planner) manda
                    // sobre releer la BD; respaldo: recopilar previos persistidos.
                    parametros["datos"] = !string.IsNullOrWhiteSpace(datoPrevio)
                        ? datoPrevio
                        : await RecopilarDatosPreviosAsync(sp, plan, paso, datoPrevio, cancellationToken);
                }
                else if (herramienta == "DocumentSearchTool")
                {
                    // Pregunta mixta (hay pasos SQL en el plan: criterio estructural
                    // por códigos propios, sin leer el lenguaje): la mitad de datos
                    // contamina el ranking documental ("trailer" + "electrónica",
                    // plan #9072). Se extrae solo la parte documental por
                    // comprensión semántica (micro-LLM, cero listas). Sin pasos
                    // SQL no hay nada que separar: va el objetivo íntegro.
                    var consultaRag = plan.Objetivo;
                    if (plan.Pasos.Any(p => p.CodigoHerramienta == "SqlQueryTool"))
                    {
                        try
                        {
                            var ollama = sp.GetService<IOllamaService>();
                            consultaRag = await ExtraerParteDocumentalAsync(ollama, plan.Objetivo, cancellationToken);
                        }
                        catch { /* respaldo: objetivo íntegro */ }
                    }
                    parametros["consulta"] = consultaRag;
                    // Preferencia del Planner (documento mencionado por código, con
                    // o sin typos): viaja en la Entrada como {"documento":"..."}. Sin
                    // ella la herramienta decide por similitud global.
                    var documentoPreferido = ExtraerDocumentoPreferido(paso.Entrada);
                    if (!string.IsNullOrWhiteSpace(documentoPreferido))
                        parametros["documento"] = documentoPreferido;
                }
                else
                {
                    // Rama paralela: el paso trae su sub-consulta en Entrada; si es
                    // null se usa el objetivo del plan. Contrato máquina-máquina
                    // (PlanBuilder.EntradaSql): {"tabla","pregunta"}. Texto plano
                    // o null en planes viejos siguen funcionando.
                    var (preguntaRama, tablaPaso) = ExtraerPreguntaYTabla(paso.Entrada, plan.Objetivo);
                    preguntaPaso = preguntaRama;
                    parametros["pregunta"] = preguntaRama;
                    // Pregunta mixta simétrica al RAG (plan #9152): si el plan también
                    // consulta documentos, la parte documental contamina los filtros
                    // ("Metas para 2027" → CostoUnitario > 2027). Se extrae solo la
                    // parte de datos por comprensión semántica (micro-LLM, cero
                    // listas). Sin pasos RAG no hay nada que separar.
                    if (plan.Pasos.Any(p => p.CodigoHerramienta == "DocumentSearchTool"))
                    {
                        try
                        {
                            var ollamaDatos = sp.GetService<IOllamaService>();
                            preguntaPaso = await ExtraerParteDatosAsync(ollamaDatos, preguntaRama, cancellationToken);
                            parametros["pregunta"] = preguntaPaso;
                        }
                        catch { /* respaldo: pregunta de la rama */ }
                    }
                    if (!string.IsNullOrWhiteSpace(tablaPaso))
                        parametros["tabla"] = tablaPaso;
                    // Rol estructural sin keywords de usuario: el PlanBuilder genera los
                    // nombres de paso con plantillas propias fijas; el modo se deriva de
                    // ellas (o del tag MODO= en planes nuevos). No se inspecciona el
                    // lenguaje del objetivo.
                    var modo = ExtraerModoPaso(paso.Descripcion, paso.Nombre);
                    parametros["modo"] = modo;
                    // Compatibilidad con planes antiguos sin tag: no se fuerza nada,
                    // la herramienta decide por LLM + esquema (nunca por keywords).
                    parametros["forzarAgregacion"] = modo == "ANALISIS";
                    parametros["forzarRaw"] = modo == "CONSULTA";
                }

                var resTool = await toolOrchestrator.EjecutarAsync(new ToolExecutionRequest
                {
                    HerramientaCodigo = herramienta,
                    Parametros = parametros,
                    IdUsuario = idUsuario,
                    IdAsistente = paso.IdAsistente,
                    PreguntaOriginal = paso.Nombre + " " + preguntaPaso
                }, cancellationToken);

                if (resTool.Exitoso)
                    return new ResultadoPasoOrquestado { Exito = true, Resultado = resTool.Contenido, TiempoMs = Transcurrido() };
                return new ResultadoPasoOrquestado { Exito = false, Error = $"{herramienta}: " + resTool.Error, TiempoMs = Transcurrido() };
            }

            // Agent (y otros): vía Agent Runtime con contexto previo de pasos anteriores.
            // El mensaje es el OBJETIVO original (con intención real); la descripción del
            // paso ("Consolida...") es vaga y hace que el modelo rehúse. Grounding genérico:
            // responder desde los resultados previos y no negar si los contienen.
            var config = await configRepo.GetAsync();
            var timeoutMs = config?.MaxTiempoTotalMs ?? 900000;

            // Clasificación de RIESGO por LLM (solo pasos de riesgo con datos). El
            // resto de Agent usa entrega determinista para no alucinar (#6038,
            // #6051), pero un paso "Clasificar por nivel de riesgo" que repite
            // el informe no sirve. Prompt acotado con salida estructurada y
            // grounding estricto; ante fallo/timeout se sigue al camino
            // determinista normal. La detección es por plantilla propia
            // ("Clasificar por nivel de riesgo" la genera el PlanBuilder), igual
            // que ExtraerModoPaso: no se inspecciona el lenguaje del usuario.
            if (EsPasoDeRiesgo(paso.Nombre))
            {
                var riesgo = await ClasificarRiesgoAsync(
                    sp, plan, paso, contextoPrevio, datoPrevio, idUsuario, timeoutMs, cancellationToken);
                if (riesgo != null)
                    return new ResultadoPasoOrquestado { Exito = true, Resultado = riesgo, TiempoMs = Transcurrido() };
            }

            // GUARD DETERMINISTA (no depender del prompt): si el plan tiene pasos
            // Tool/RAG previos y NINGUNO dejó datos utilizables, no se llama al LLM.
            // Plan #6038: los dos pasos SqlQueryTool terminaron en Error y el agente
            // "entregó" tablas inventadas (categorías y montos que no existen) pese a
            // que el prompt lo prohibía. deepseek-r1:7b no respeta esa instrucción.
            // Criterio estructural: Estado del paso + resultado vacío + marcadores de
            // "sin datos" ya definidos. No se inspecciona el lenguaje del objetivo.
            if (await SinDatosPreviosAsync(sp, plan, paso, cancellationToken))
            {
                return new ResultadoPasoOrquestado
                {
                    Exito = false,
                    Error = "No hay datos de pasos previos para entregar: los pasos que debían " +
                            "consultar la información fallaron o devolvieron vacío. No se genera " +
                            "respuesta para no inventar resultados.",
                    TiempoMs = Transcurrido()
                };
            }

            // ENTREGA DETERMINISTA (sin LLM): si el contexto previo ya trae el reporte
            // consolidado de ReportTool ("# ..." + "_Generado: ..."), se entrega tal
            // cual. Las cifras ya las calculó SQL; pasarlas por el LLM solo añade
            // latencia (minutos en CPU), typos y riesgo de fuga de instrucciones
            // (plan #6051) o cifras inventadas (#6038). Sin reporte, se usa el LLM.
            var entregaDirecta = ExtraerReporteConsolidado(contextoPrevio);
            if (!string.IsNullOrWhiteSpace(entregaDirecta))
            {
                _logger.LogInformation("Orchestrator: entrega final determinista sin LLM ({Len} caracteres).", entregaDirecta.Length);
                return new ResultadoPasoOrquestado { Exito = true, Resultado = entregaDirecta, TiempoMs = Transcurrido() };
            }

            // ENTREGA ESTRUCTURADA DETERMINISTA (plan #9066, sin LLM): si el contexto
            // trae marcadores propios (totales/filas SQL, sección RAG o token de
            // contrato) pero ningún reporte consolidado —típico plan mixto sin paso
            // de informe—, se formatea con GenerarResumenEjecutivo en vez de pedirle
            // al LLM que fusione: el modelo pequeño parafraseaba e inventaba ítems
            // ("Pruéncipes") y disclaimers falsos. El RAG se reproduce verbatim.
            var entregaEstructurada = EntregaEstructuradaDeterminista(contextoPrevio);
            if (!string.IsNullOrWhiteSpace(entregaEstructurada))
            {
                _logger.LogInformation("Orchestrator: entrega estructurada determinista sin LLM ({Len} caracteres).", entregaEstructurada.Length);
                return new ResultadoPasoOrquestado { Exito = true, Resultado = entregaEstructurada, TiempoMs = Transcurrido() };
            }

            // El contexto previo acumula todos los pasos (filas + reportes) y en CPU
            // el thinking supera el timeout. Se conserva la COLA (lo más reciente =
            // el reporte consolidado, que ya incluye totales y filas): estructural,
            // sin inspeccionar contenido.
            var contextoRecortado = RecortarContexto(contextoPrevio, 3000);
            var respTask = chatService.ProcesarMensajeAsync(new MensajeRequest
            {
                IdAsistente = paso.IdAsistente,
                Mensaje = "ENTREGA FINAL: presenta los resultados previos como respuesta definitiva " +
                    "(tablas, datos o texto: reprodúcelos y preséntalos). Está prohibido decir que falta " +
                    "información si los contienen, y prohibido describir pasos futuros o planes de acción: " +
                    "el trabajo ya está hecho, solo entrégalo. " +
                    "IMPORTANTE: si el contexto contiene 'Total filtrado: X' o 'Total: X', usa ese valor exacto " +
                    "como el total de registros, no cuentes las filas visibles. Si el contexto NO contiene " +
                    "ningún resultado (vacío o solo coordinación), dilo explícitamente y NO inventes " +
                    "cifras ni tablas. Objetivo original: " + plan.Objetivo,
                UsuarioPropietario = idUsuario,
                EsEjecucionPlan = true,
                ContextoAgente = contextoRecortado
            }, cancellationToken);

            var completed = await Task.WhenAny(respTask, Task.Delay(timeoutMs, cancellationToken));
            if (completed != respTask)
                throw new TimeoutException($"El paso '{paso.Nombre}' no respondió en {timeoutMs} ms.");

            var resp = await respTask;
            if (resp == null || !resp.Exitoso)
                return new ResultadoPasoOrquestado
                {
                    Exito = false,
                    Error = resp?.Error ?? "Sin respuesta del agente.",
                    TiempoMs = Transcurrido()
                };
            // Sanea ecos del prompt interno (plan #6051): el modelo a veces devuelve
            // las instrucciones del sistema como si fueran contenido.
            return new ResultadoPasoOrquestado { Exito = true, Resultado = SanearEntrega(resp.Respuesta), TiempoMs = Transcurrido() };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orchestrator: fallo al ejecutar paso validado '{Paso}' del plan {Plan}.", paso.Nombre, plan.IdPlan);
            return new ResultadoPasoOrquestado { Exito = false, Error = $"Error al ejecutar {paso.CodigoHerramienta ?? paso.Tipo}: " + ex.Message, TiempoMs = Transcurrido() };
        }
    }

    /// <summary>
    /// True solo cuando el plan TIENE pasos Tool/RAG previos y ninguno dejó un resultado
    /// utilizable (falló, quedó vacío o devolvió un marcador de "sin datos"). Un plan sin
    /// Tool/RAG (p. ej.answered puramente documental) NO se corta: el agente debe responder.
    /// </summary>
    internal static async Task<bool> SinDatosPreviosAsync(
        IServiceProvider sp, Plan plan, PlanStep paso, CancellationToken ct)
    {
        try
        {
            var stepRepo = sp.GetRequiredService<IPlanStepRepository>();
            var previos = (await stepRepo.GetByPlanAsync(plan.IdPlan, ct, true))
                .Where(p => p.Orden < paso.Orden && (p.Tipo == "Tool" || p.Tipo == "RAG"))
                .ToList();

            // Sin Tool/RAG previos no hay nada que entregar: deja responder al agente.
            if (previos.Count == 0) return false;

            return !previos.Any(p => p.Estado == "Completado"
                                     && !string.IsNullOrWhiteSpace(p.Resultado)
                                     && !(p.Tipo == "RAG" && EsMarcadorSinDatosRag(p.Resultado))
                                     && !(p.Tipo == "Tool" && EsMarcadorSinDatosTool(p.Resultado)));
        }
        catch (Exception ex)
        {
            // Ante cualquier duda se deja pasar al LLM (no bloquear la entrega).
            sp.GetService<ILogger<AgentOrchestrator>>()
              ?.LogWarning(ex, "Orchestrator: no se pudo verificar si hay datos previos del plan {IdPlan}.", plan.IdPlan);
            return false;
        }
    }

    /// <summary>
    /// Entrega estructurada sin LLM para contextos con marcadores propios pero sin
    /// reporte consolidado (plan mixto sin paso de informe, #9066). Reutiliza el
    /// formateador determinista de ReportTool: totales SQL exactos + RAG verbatim.
    /// Null si no hay marcadores (conversación libre → LLM como antes).
    /// </summary>
    internal static string? EntregaEstructuradaDeterminista(string? contexto)
    {
        if (string.IsNullOrWhiteSpace(contexto)) return null;
        if (!ReportTool.EsResultadoEstructurado(contexto)) return null;
        var md = SanearEntrega(ReportTool.GenerarResumenEjecutivo(contexto, "Resultado final"));
        return string.IsNullOrWhiteSpace(md) ? null : md;
    }

    /// <summary>
    /// Reporte consolidado dentro del contexto previo: ReportTool.GenerarResumenEjecutivo
    /// siempre emite "# {titulo}" seguido de "_Generado: ...". Se devuelve desde el ÚLTIMO
    /// encabezado (lo más reciente = consolidado). Sin ese contrato, null (usar LLM).
    /// Criterio estructural (formato propio), sin inspeccionar lenguaje del usuario.
    /// </summary>
    internal static string? ExtraerReporteConsolidado(string? contexto)
    {
        if (string.IsNullOrWhiteSpace(contexto)) return null;
        var lineas = contexto.Split('\n');
        var inicio = -1;
        for (var i = 0; i < lineas.Length; i++)
        {
            if (!lineas[i].StartsWith("# ", StringComparison.Ordinal)) continue;
            for (var j = i + 1; j < Math.Min(i + 4, lineas.Length); j++)
            {
                if (lineas[j].Contains("_Generado:", StringComparison.Ordinal)) { inicio = i; break; }
            }
        }
        if (inicio < 0) return null;
        var reporte = SanearEntrega(string.Join("\n", lineas.Skip(inicio)).Trim());
        if (string.IsNullOrWhiteSpace(reporte)) return null;
        // Compuerta de sustancia: un "reporte" sin datos (ReportTool con insumo vacío
        // que ecoa la pregunta, #7056) no debe atajar al LLM: la entrega por LLM con
        // el contexto completo sí incluye lo documental. Marcadores estructurales
        // propios: tablas '|', desgloses '- ', totales o fuentes RAG.
        var tieneSustancia = reporte.Contains('|')
            || reporte.Contains("\n- ", StringComparison.Ordinal)
            || reporte.Contains("Total", StringComparison.OrdinalIgnoreCase)
            || reporte.Contains("[Fuente:", StringComparison.Ordinal)
            || reporte.Contains("Desglose", StringComparison.OrdinalIgnoreCase);
        if (!tieneSustancia) return null;
        return reporte;
    }

    /// <summary>¿Es un paso de clasificación de riesgo? Plantilla propia del
    /// PlanBuilder ("Clasificar por nivel de riesgo"), igual que ExtraerModoPaso:
    /// se deriva de nombres que el propio sistema genera, nunca del lenguaje
    /// del usuario.</summary>
    internal static bool EsPasoDeRiesgo(string? nombre)
        => !string.IsNullOrWhiteSpace(nombre)
            && nombre.Contains("riesgo", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Presupuesto de espera para la clasificación de riesgo. deepseek-r1:7b sin
    /// GPU tarda 80-150 s, así que un MaxTiempoTotalMs corto (60 s) cortaba la
    /// llamada y el paso caía al determinista repitiendo el informe (#1007, #1008):
    /// se observaba "Timeout al comunicarse con Ollama" y luego
    /// "entrega estructurada determinista". Se toma el MAYOR entre el presupuesto
    /// configurado y un piso de 5 min, con techo en 10 min para no pasarse del
    /// CTS de 15 min del plan.
    /// </summary>
    internal static int CalcularPresupuestoClasificacion(int timeoutMs)
        => timeoutMs >= 600000 ? timeoutMs : Math.Max(timeoutMs, 300000);

    /// <summary>
    /// Clasifica por nivel cada hallazgo del contexto previo usando el LLM, con
    /// salida estructurada y grounding estricto. Devuelve null si no hay datos
    /// o si el modelo falla/excede el tiempo: en ese caso el llamador sigue al
    /// camino determinista. Sin vocabulario de dominio: el prompt pide niveles
    /// y motivos a partir de lo que el contexto ya trae.
    /// </summary>
    private async Task<string?> ClasificarRiesgoAsync(
        IServiceProvider sp, Plan plan, PlanStep paso,
        string? contextoPrevio, string? datoPrevio, int idUsuario,
        int timeoutMs, CancellationToken ct)
    {
        try
        {
            var contexto = !string.IsNullOrWhiteSpace(datoPrevio) ? datoPrevio : contextoPrevio;
            if (string.IsNullOrWhiteSpace(contexto)) return null;
            var chatService = sp.GetRequiredService<IChatService>();
            var espera = CalcularPresupuestoClasificacion(timeoutMs);
            var respTask = chatService.ProcesarMensajeAsync(new MensajeRequest
            {
                IdAsistente = paso.IdAsistente,
                Mensaje = "CLASIFICACIÓN DE RIESGOS: a partir UNICAMENTE de los datos del contexto, "
                    + "lista cada riesgo en una línea con este formato exacto: '- [ALTO|MEDIO|BAJO] hecho: motivo en una línea'. "
                    + "REGLAS OBLIGATORIAS: "
                    + "(1) Copia los valores exactamente como aparecen en el contexto; prohibido inventar o redondear cifras. "
                    + "(2) Si comparas dos valores de la misma fila, declara riesgo SOLO si el primero es numéricamente MENOR "
                    + "que el segundo; si es mayor o igual, NO lo reportes como riesgo. "
                    + "(3) No menciones entidades, campos, categorías ni relaciones que no aparezcan literalmente en el contexto "
                    + "(por ejemplo, nada de proveedores, responsables ni plazos si el contexto no los da). "
                    + "(4) Un solo riesgo por línea y sin repetir el mismo dato en varias líneas. "
                    + "Si ningún dato cumple esas reglas, responde exactamente 'Sin riesgos evidentes en los datos.'. "
                    + "No describas pasos futuros ni planes de acción. Objetivo original: " + plan.Objetivo,
                UsuarioPropietario = idUsuario,
                EsEjecucionPlan = true,
                ContextoAgente = contexto.Length > 6000 ? contexto[^6000..] : contexto
            }, ct);
            var completed = await Task.WhenAny(respTask, Task.Delay(espera, ct));
            if (completed != respTask)
            {
                _logger.LogWarning(
                    "Orchestrator: la clasificación de riesgo del paso '{Paso}' no respondió en {Espera} ms; se sigue al camino determinista.",
                    paso.Nombre, espera);
                return null;
            }
            var resp = await respTask;
            if (resp == null || !resp.Exitoso || string.IsNullOrWhiteSpace(resp.Respuesta))
            {
                _logger.LogWarning(
                    "Orchestrator: la clasificación de riesgo del paso '{Paso}' no devolvió salida utilizable (Exitoso={Exitoso}); se sigue al camino determinista.",
                    paso.Nombre, resp?.Exitoso ?? false);
                return null;
            }

            // Filtro determinista de groundaje (#1009): deepseek-r1:7b declaraba
            // "stock por debajo del mínimo" con 25 vs 10, es decir comparando al revés.
            // El prompt de arriba lo pide, pero el modelo no siempre obedece, así que
            // además se descarta toda línea cuya afirmación no sea verificable contra
            // el contexto. Sin vocabulario de dominio: el contraste es textual/numérico.
            var filtrada = ValidarGroundajeRiesgo(SanearEntrega(resp.Respuesta), contexto);
            if (string.IsNullOrWhiteSpace(filtrada))
            {
                _logger.LogWarning(
                    "Orchestrator: la clasificación de riesgo del paso '{Paso}' no superó el control de groundaje; se sigue al camino determinista.",
                    paso.Nombre);
                return null;
            }
            return filtrada;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orchestrator: clasificación de riesgo falló para el paso '{Paso}'; se sigue determinista.", paso.Nombre);
            return null;
        }
    }

    /// <summary>
    /// Control de groundaje para la clasificación de riesgo. Es puramente
    /// estructural: no mira el significado de las palabras, solo que cada línea
    /// (a) tenga el formato '- [NIVEL] ...', (b) NO afirme que un número es menor
    /// que otro cuando en el contexto ese mismo par aparece con el primero mayor o
    /// igual, y (c) no contenga cifras ausentes del contexto.
    /// Así se corta la comparación invertida que producía el modelo 7B (#1009)
    /// sin introducir reglas de negocio ni vocabulario del dominio.
    /// Si todo se descarta devuelve cadena vacía para que el llamador use el
    /// camino determinista.
    /// </summary>
    internal static string ValidarGroundajeRiesgo(string? respuesta, string? contexto)
    {
        if (string.IsNullOrWhiteSpace(respuesta)) return string.Empty;
        var ctx = contexto ?? string.Empty;

        // Cifras presentes en el contexto (normalizando separadores de miles).
        var cifrasCtx = new HashSet<string>(
            Regex.Matches(NormalizarCifras(ctx), @"\d+(?:\.\d+)?").Select(m => m.Value),
            StringComparer.Ordinal);

        var lineas = new List<string>();
        foreach (var raw in respuesta.Split('\n'))
        {
            var linea = raw.Trim();
            if (linea.Length == 0) continue;
            if (!linea.StartsWith("- [", StringComparison.Ordinal)) continue;
            if (!Regex.IsMatch(linea, @"^-\s*\[(ALTO|MEDIO|BAJO)\]", RegexOptions.IgnoreCase)) continue;

            // (c) ninguna cifra inventada
            var cifrasLinea = Regex.Matches(NormalizarCifras(linea), @"\d+(?:\.\d+)?")
                               .Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();
            if (cifrasLinea.Any(c => !cifrasCtx.Contains(c))) continue;

            // (b) comparación invertida: "X (a) Y" donde el modelo afirma inferioridad
            var m = Regex.Match(linea, @"(\d+(?:\.\d+)?)\s*(?:<|<=|menor que|por debajo de|inferior a)\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (m.Success
                && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var izq)
                && double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var der)
                && izq >= der)
                continue;

            lineas.Add(linea);
        }

        // Una sola línea por riesgo: si el mismo par de cifras ya fue reportado, se
        // descarta el duplicado (el modelo repitió el mismo hallazgo 3 veces en #1009).
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        var únicas = new List<string>();
        foreach (var l in lineas)
        {
            var cifras = string.Join(",", Regex.Matches(NormalizarCifras(l), @"\d+(?:\.\d+)?").Select(x => x.Value));
            if (vistas.Add(cifras)) únicas.Add(l);
        }

        if (únicas.Count == 0)
            return respuesta.Contains("Sin riesgos evidentes", StringComparison.OrdinalIgnoreCase)
                ? "Sin riesgos evidentes en los datos."
                : string.Empty;

        return "**Clasificación de riesgos**\n\n" + string.Join("\n", únicas);
    }

    /// <summary>Quita separadores de miles y el símbolo de moneda para comparar
    /// cifras entre el contexto y la respuesta del LLM (4,50 -> 4.50; 2,723.50 -> 2723.50).</summary>
    private static string NormalizarCifras(string texto)
        => Regex.Replace(texto, @"(?<=\d),(?=\d{3}\b)", string.Empty);

    /// <summary>
    /// Quita líneas de instrucciones internas que el LLM a veces ecoa en su respuesta
    /// (fuga de prompt del plan #6051). Son marcadores propios del sistema, nunca datos
    /// de BD, así que filtrarlos no altera cifras.
    /// </summary>
    internal static string SanearEntrega(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto ?? string.Empty;
        string[] marcadores =
        [
            "INSTRUCCIONES OBLIGATORIAS",
            "CONTEXT DE DATOS REALES",
            "IMPORTANTE: El Motor de Herramientas",
            "Instrucciones OBLIGATORIAS de transcripción",
            "## RESULTADO DE HERRAMIENTA DEL MOTOR",
            "## CONTEXT DE DATOS"
        ];
        var limpias = texto.Split('\n')
            .Where(l => !marcadores.Any(m => l.Contains(m, StringComparison.OrdinalIgnoreCase)));
        return string.Join("\n", limpias).Trim();
    }

    /// <summary>
    /// Recorta el contexto previo por la COLA (lo más reciente primero). El reporte
    /// consolidado va al final y ya contiene totales + filas, así que nada se pierde.
    /// El corte cae en un salto de línea, nunca a media palabra: con varias capas SQL
    /// el bloque literal empezaba en "lumna decimal 631650.00)" (plan #12197).
    /// </summary>
    internal static string? RecortarContexto(string? contexto, int maxChars)
    {
        if (string.IsNullOrEmpty(contexto) || contexto.Length <= maxChars) return contexto;
        var cola = contexto[^maxChars..];
        var corte = cola.IndexOf('\n');
        // Si el resto no cabe ni tras saltar la línea parcial, se entrega igual:
        // preferimos texto truncado a perder el contexto por completo.
        if (corte >= 0 && corte < cola.Length - 1) cola = cola[(corte + 1)..];
        return "[contexto recortado: se conserva lo más reciente]\n" + cola;
    }

    /// <summary>
    /// Extrae la parte documental de una solicitud mixta por comprensión semántica
    /// (micro-LLM genérico, sin listas): lo que pregunta por documentos o contenido
    /// documentado. Si todo es documental o el LLM falla, devuelve el objetivo
    /// íntegro. Nunca inspecciona palabras concretas.
    /// </summary>
    internal static async Task<string> ExtraerParteDocumentalAsync(
        IOllamaService? ollama, string objetivo, CancellationToken ct)
    {
        if (ollama == null || string.IsNullOrWhiteSpace(objetivo))
            return objetivo;
        try
        {
            var historial = new List<Mensaje>
            {
                new Mensaje
                {
                    Rol = RolMensaje.User,
                    Contenido = "La solicitud mezcla una pregunta sobre documentos con otra sobre datos. " +
                        "Extrae SOLO la parte que pregunta por documentos o contenido documentado, " +
                        "copiándola casi literal. Responde SOLO este JSON: {\"documental\":\"...\"}. " +
                        "Si toda la solicitud es documental, repítela completa. Solicitud: " + objetivo
                }
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            var respuesta = await ollama.SendMessageAsync(historial, null, null, 0.0, 200, cts.Token);
            if (!string.IsNullOrWhiteSpace(respuesta))
            {
                var inicio = respuesta.IndexOf('{');
                var fin = respuesta.LastIndexOf('}');
                if (inicio >= 0 && fin > inicio)
                {
                    var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                        respuesta[inicio..(fin + 1)],
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (doc != null && doc.TryGetValue("documental", out var parte)
                        && !string.IsNullOrWhiteSpace(parte) && parte.Length >= 8)
                        return parte.Trim();
                }
            }
        }
        catch { /* respaldo: objetivo íntegro */ }
        return objetivo;
    }

    /// <summary>
    /// Parte de DATOS de una solicitud mixta (micro-LLM genérico, sin listas):
    /// lo que pregunta por registros, cantidades, filtros o cifras de la base.
    /// Simétrico a ExtraerParteDocumentalAsync: cada rama (SQL y RAG) trabaja con
    /// su mitad para que los números de una no contaminen los filtros de la otra
    /// (plan #9152). Si todo es de datos o el LLM falla, devuelve el texto íntegro.
    /// </summary>
    internal static async Task<string> ExtraerParteDatosAsync(
        IOllamaService? ollama, string texto, CancellationToken ct)
    {
        if (ollama == null || string.IsNullOrWhiteSpace(texto))
            return texto;
        // Los pasos CONSULTA + ANALISIS de un mismo plan comparten texto: sin caché
        // se pagaría el micro-LLM dos veces (CPU, NUM_PARALLEL=1). Clave estructural.
        var clave = System.Text.RegularExpressions.Regex.Replace(
            texto.ToLowerInvariant().Trim(), @"\s+", " ");
        if (_cacheParteDatos.TryGetValue(clave, out var hit) && hit.Expira > DateTime.UtcNow)
            return hit.Parte;
        try
        {
            var historial = new List<Mensaje>
            {
                new Mensaje
                {
                    Rol = RolMensaje.User,
                    Contenido = "La solicitud mezcla una pregunta sobre datos con otra sobre documentos. " +
                        "Extrae SOLO la parte que pregunta por datos (registros, cantidades, filtros, cifras), " +
                        "copiándola casi literal. Responde SOLO este JSON: {\"datos\":\"...\"}. " +
                        "Si toda la solicitud es de datos, repítela completa. Solicitud: " + texto
                }
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            var respuesta = await ollama.SendMessageAsync(historial, null, null, 0.0, 200, cts.Token);
            if (!string.IsNullOrWhiteSpace(respuesta))
            {
                var inicio = respuesta.IndexOf('{');
                var fin = respuesta.LastIndexOf('}');
                if (inicio >= 0 && fin > inicio)
                {
                    var doc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                        respuesta[inicio..(fin + 1)],
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (doc != null && doc.TryGetValue("datos", out var parte)
                        && !string.IsNullOrWhiteSpace(parte) && parte.Length >= 8)
                    {
                        var recorte = parte.Trim();
                        _cacheParteDatos[clave] = (DateTime.UtcNow.AddMinutes(10), recorte);
                        return recorte;
                    }
                }
            }
        }
        catch { /* respaldo: texto íntegro */ }
        return texto;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, string Parte)> _cacheParteDatos
        = new(System.StringComparer.Ordinal);

    /// <summary>
    /// Rol estructural del paso SQL. Se deriva de plantillas propias del PlanBuilder
    /// (nombres fijos "Consultar datos de ..."/"Analizar resultados...") o del tag
    /// "MODO=" en Descripcion. No inspecciona el lenguaje del usuario: cero keywords.
    /// Planes antiguos sin tag ni plantilla devuelven "AUTO".
    /// </summary>
    private static string ExtraerModoPaso(string? descripcion, string? nombre)
    {
        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            var d = descripcion.TrimStart();
            if (d.StartsWith("MODO=CONSULTA", StringComparison.OrdinalIgnoreCase)) return "CONSULTA";
            if (d.StartsWith("MODO=ANALISIS", StringComparison.OrdinalIgnoreCase)) return "ANALISIS";
        }
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            if (nombre.StartsWith("Consultar datos de ", StringComparison.OrdinalIgnoreCase)) return "CONSULTA";
            // El Planner puede sufijar la tabla ("... indicadores [Activos]") en
            // planes multi-tabla: el prefijo manda, no el nombre exacto.
            if (nombre.StartsWith("Analizar resultados y calcular indicadores", StringComparison.OrdinalIgnoreCase)) return "ANALISIS";
        }
        return "AUTO";
    }

    /// <summary>
    /// Entrada de un paso Tool: o texto plano (sub-pregunta de una rama o nada →
    /// objetivo del plan) o contrato JSON {"tabla","pregunta"} del Planner. La
    /// tabla viaja aparte para no contaminar la pregunta con JSON.
    /// </summary>
    /// <summary>Lee el código de documento del contrato {"documento":"..."}.
/// Devuelve null si no es ese contrato; nunca lanza.</summary>
    private static string? ExtraerDocumentoDeEntrada(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return null;
        var texto = entrada.Trim();
        if (!texto.StartsWith('{')) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(texto);
            if (doc.RootElement.TryGetProperty("documento", out var d))
                return d.GetString();
        }
        catch { }
        return null;
    }

    private static (string Pregunta, string? Tabla) ExtraerPreguntaYTabla(string? entrada, string objetivo)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return (objetivo, null);
        var texto = entrada.Trim();
        if (texto.StartsWith('{'))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(texto);
                var raiz = doc.RootElement;
                var pregunta = raiz.TryGetProperty("pregunta", out var p) ? p.GetString() : null;
                var tabla = raiz.TryGetProperty("tabla", out var t) ? t.GetString() : null;
                if (!string.IsNullOrWhiteSpace(pregunta))
                    return (pregunta, string.IsNullOrWhiteSpace(tabla) ? null : tabla);
            }
            catch { /* no es JSON: texto plano */ }
        }
        return (entrada, null);
    }

    /// <summary>
    /// Documento preferido de un paso RAG, si el Planner lo resolvió por mención
    /// (contrato {"documento":"..."}). Texto plano o null en planes viejos →
    /// null y la herramienta decide por similitud.
    /// </summary>
    internal static string? ExtraerDocumentoPreferido(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return null;
        var texto = entrada.Trim();
        if (!texto.StartsWith('{')) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(texto);
            if (doc.RootElement.TryGetProperty("documento", out var d))
            {
                var codigo = d.GetString();
                if (!string.IsNullOrWhiteSpace(codigo)) return codigo.Trim();
            }
        }
        catch { /* no es JSON: sin preferencia */ }
        return null;
    }

    /// <summary>
    /// Datos para el reporte: concatena los resultados de TODOS los pasos Tool/RAG
    /// previos (filas + totales), no solo el inmediato anterior. Criterio puramente
    /// estructural (Orden y Tipo): sin inspeccionar lenguaje, sin keywords.
    /// Excluye pasos RAG que devolvieron marcadores de "sin datos" SOLO cuando hay
    /// pasos Tool (SQL) con datos disponibles. Si no hay datos SQL, se incluye RAG
    /// aunque esté vacío para que el reporte falle correctamente.
    /// Tope de 4000 caracteres para no saturar el PDF ni el contexto posterior.
    /// </summary>
    private async Task<string> RecopilarDatosPreviosAsync(
        IServiceProvider sp, Plan plan, PlanStep paso, string? datoPrevio, CancellationToken ct)
    {
        try
        {
            var stepRepo = sp.GetRequiredService<IPlanStepRepository>();
            var todosPrevios = (await stepRepo.GetByPlanAsync(plan.IdPlan, ct, true))
                .Where(p => p.Orden < paso.Orden && (p.Tipo == "Tool" || p.Tipo == "RAG") && !string.IsNullOrWhiteSpace(p.Resultado))
                .ToList();

            // DEBUG: Log para ver qué pasos se encontraron
            _logger.LogInformation("RecopilarDatosPrevios: encontrados {Count} pasos previos", todosPrevios.Count);
            foreach (var p in todosPrevios)
            {
                _logger.LogInformation("  Paso {Orden} ({Tipo}): {Nombre} - EsMarcadorTool={EsTool}, EsMarcadorRag={EsRag}",
                    p.Orden, p.Tipo, p.Nombre,
                    p.Tipo == "Tool" ? EsMarcadorSinDatosTool(p.Resultado) : false,
                    p.Tipo == "RAG" ? EsMarcadorSinDatosRag(p.Resultado) : false);
            }

            // Solo excluimos RAG vacío si hay datos SQL disponibles
            var tieneDatosSql = todosPrevios.Any(p => p.Tipo == "Tool" && !EsMarcadorSinDatosTool(p.Resultado));
            _logger.LogInformation("RecopilarDatosPrevios: tieneDatosSql={Tiene}", tieneDatosSql);

            var previos = todosPrevios
                .Where(p => !(p.Tipo == "RAG" && EsMarcadorSinDatosRag(p.Resultado) && tieneDatosSql))
                .OrderBy(p => p.Orden)
                .Select(p => $"[Paso {p.Orden}: {p.Nombre}]\n{p.Resultado}")
                .ToList();

            _logger.LogInformation("RecopilarDatosPrevios: después de filtrar, {Count} pasos para reporte", previos.Count);

            if (previos.Count > 0)
            {
                var combinado = string.Join("\n", previos);
                const int max = 4000;
                if (combinado.Length > max)
                    combinado = combinado[..max] + "\n… (datos truncados)";
                return combinado;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Orchestrator: no se pudieron recopilar datos previos para el reporte.");
        }
        return datoPrevio ?? plan.Objetivo;
    }

    /// <summary>
    /// Detecta marcadores de "sin datos" específicos de RAG para excluirlos del reporte.
    /// Solo aplica a pasos RAG; los pasos Tool con datos reales no se filtran.
    /// </summary>
    /// <summary>
    /// ¿Un paso RAG trajo datos? Se decide por el CONTRATO compartido, no por la frase
    /// del mensaje: comparar el texto del error es frágil (depende del wording) y esa
    /// lista se desincronizaba entre componentes.
    /// </summary>
    private static bool EsMarcadorSinDatosRag(string resultado)
        => ContratoResultado.TodosSonSinDatos(resultado);

    /// <summary>
    /// ¿Un paso Tool (SQL) trajo datos? Mismo contrato. Antes comparaba contra cuatro
    /// frases concretas ("no se pudo determinar", "consulta rechazada"...): si un mensaje
    /// de error cambiaba de redacción, el paso se tomaba por datos válidos y el agente
    /// final entregaba una tabla inventada encima.
    /// </summary>
    private static bool EsMarcadorSinDatosTool(string resultado)
        => ContratoResultado.TodosSonSinDatos(resultado);

    private static string GenerarTextoCoordinacion(Plan plan)
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

    private ExecutionGraph ConstruirGrafo(int idExecution, Asistente.Domain.Entities.Asistente principal, List<AgentCandidate> candidatos, AgentRequest request, ConfiguracionOrchestrator? config)
    {
        var grafo = new ExecutionGraph();
        var maxAgentes = config?.MaxAgentesPorSolicitud ?? 5;
        var limite = Math.Min(candidatos.Count, Math.Max(0, maxAgentes - 1)); // -1 por el principal

        // Nodo principal (siempre primero)
        grafo.Nodos.Add(new ExecutionNode
        {
            IdNodo = 0,
            IdAgente = principal.IdAsistente,
            NombreAgente = principal.Nombre,
            Accion = "Coordinar y responder como agente principal",
            PreguntaAsignada = request.Pregunta
        });

        var idNodo = 1;
        foreach (var c in candidatos.Take(limite))
        {
            // Cada colaborador recibe la pregunta con la instrucción de SU capacidad,
            // que viene de Herramientas.Descripcion (configuración), no de una plantilla
            // escrita en código por tipo de rol. Así una capacidad nueva —o una base de
            // datos nueva— no requiere añadir un caso aquí: su descripción ES la
            // instrucción. Sin esto todos responden lo mismo (el primer short-circuit
            // determinista) y el duplicado no se elimina.
            var preguntaPorRol = ConstruirInstruccionRol(c, request.Pregunta);
            grafo.Nodos.Add(new ExecutionNode
            {
                IdNodo = idNodo++,
                IdAgente = c.IdAgente,
                NombreAgente = c.Nombre,
                Accion = $"Colaborar ({c.Rol})",
                PreguntaAsignada = preguntaPorRol,
                Alcance = c.Alcance,
                DependeDe = c.DependeDe.Contains(principal.IdAsistente) ? new List<int> { 0 } : new List<int>()
            });
        }

        return grafo;
    }

    /// <summary>
    /// Instrucción de un colaborador, sin vocabulario fijo por rol: usa la descripción de
    /// la herramienta que motivó su selección (dato de configuración) y, si no hay, el
    /// objetivo configurado del agente. Nunca se decide por palabras del texto.
    /// </summary>
    private static string ConstruirInstruccionRol(AgentCandidate c, string pregunta)
    {
        if (!string.IsNullOrWhiteSpace(c.InstruccionRol))
            return $"Capacidad asignada: {c.InstruccionRol.Trim()}\nSolicitud: {pregunta}";
        if (!string.IsNullOrWhiteSpace(c.Objetivo))
            return $"{c.Objetivo}\nSolicitud: {pregunta}";
        return pregunta;
    }

    /// <summary>
    /// Ejecuta UN nodo con aislamiento total: construye el contexto y corre el nodo
    /// usando ÚNICAMENTE los servicios de ESTA instancia (resuelta por llamada desde
    /// un scope propio por rama). Permite nodos en paralelo sin compartir DbContext.
    /// </summary>
    public async Task EjecutarNodoAisladoAsync(
        AgentExecution execution, ExecutionNode nodo, SharedContext contextoGlobal,
        string? contextoPrevioPlan, int[] ordenWrap, ConfiguracionOrchestrator? config,
        CancellationToken cancellationToken = default)
    {
        var ctxAgente = await _contextManager.BuildContextForAgentAsync(
            nodo.IdAgente, contextoGlobal, cancellationToken);
        // Igual que el camino secuencial: el contexto previo del plan manda.
        ctxAgente.ContextoPrevio = contextoPrevioPlan;
        await EjecutarNodoAsync(execution, nodo, ctxAgente, ordenWrap, config, cancellationToken);
    }

    private async Task EjecutarNodoAsync(
        AgentExecution execution, ExecutionNode nodo, AgentContext ctxAgente,
        int[] ordenWrap, ConfiguracionOrchestrator? config, CancellationToken cancellationToken)
    {
        nodo.Estado = "EnEjecucion";
        var inicio = DateTime.UtcNow;

        var step = new AgentExecutionStep
        {
            IdExecution = execution.IdExecution,
            Orden = System.Threading.Interlocked.Increment(ref ordenWrap[0]) - 1,
            IdAgente = nodo.IdAgente,
            Accion = nodo.Accion,
            Estado = "EnEjecucion",
            Dependencias = JsonSerializer.Serialize(nodo.DependeDe)
        };
        step = await _stepRepo.AddAsync(step, cancellationToken);

        try
        {
            // Regla 1: el Orchestrator ejecuta el agente vía el Agent Runtime (IChatService),
            // NUNCA un agente invoca directamente a otro.
            // Timeout de seguridad: si el Agent Runtime (Ollama en CPU) no responde,
            // abortamos el nodo para no dejar la ejecución colgada en "EnProceso".
            var timeoutMs = config?.MaxTiempoTotalMs ?? 120000;
            var respTask = _chatService.ProcesarMensajeAsync(new MensajeRequest
            {
                IdAsistente = nodo.IdAgente,
                Mensaje = nodo.PreguntaAsignada,
                UsuarioPropietario = execution.IdUsuario,
                // Alcance por capacidad (dato del nodo, no texto parseado): el nodo
                // documental no dispara SQL y el de datos no recupera documentos.
                Alcance = nodo.Alcance,
                // Solo se omite RAG/contexto empresarial (modo plan rapido) cuando hay
                // contexto previo con datos reales. Sin el, el nodo debe recuperar solo
                // o alucina (nombres/filas inventadas).
                EsEjecucionPlan = !string.IsNullOrWhiteSpace(ctxAgente.ContextoPrevio),
                ContextoAgente = ctxAgente.ContextoPrevio
            }, cancellationToken);

            var completed = await Task.WhenAny(respTask, Task.Delay(timeoutMs, cancellationToken));
            if (completed != respTask)
                throw new TimeoutException($"El agente {nodo.NombreAgente} no respondió en {timeoutMs} ms (posiblemente Ollama en CPU saturado).");

            var resp = await respTask;

            // Si el Agent Runtime (ChatService) devolvió una respuesta fallida (p.ej. Ollama
            // caído) SIN lanzar excepción, el nodo debe marcarse como Error para que la
            // estrategia de error (Cancelar) aborte el grafo y el Planner pueda reintentar.
            // Sin esto, el Orchestrator trataría el nodo como "Completado" silenciosamente.
            if (resp == null || !resp.Exitoso)
                throw new InvalidOperationException(
                    $"Agente {nodo.NombreAgente} falló: {resp?.Error ?? "sin respuesta del agente"}");

            nodo.TiempoMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
            nodo.Resultado = resp.Respuesta;
            nodo.Estado = "Completado";

            step.Resultado = resp.Respuesta;
            step.TiempoMs = nodo.TiempoMs;
            step.Estado = "Completado";
            await _stepRepo.UpdateAsync(step, cancellationToken);

            await RegistrarTrazaAsync(execution.IdExecution, "EjecucionNodo",
                $"Agente {nodo.NombreAgente} completó '{nodo.Accion}' en {nodo.TiempoMs}ms.", cancellationToken);
        }
        catch (Exception ex)
        {
            nodo.TiempoMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;
            nodo.Estado = "Error";
            nodo.Error = ex.Message;
            step.Estado = "Error";
            step.Error = ex.Message;
            step.TiempoMs = nodo.TiempoMs;
            await _stepRepo.UpdateAsync(step, cancellationToken);

            await RegistrarTrazaAsync(execution.IdExecution, "ErrorNodo",
                $"Agente {nodo.NombreAgente} falló: {ex.Message}", cancellationToken);

            // Estrategia de error (Actividad 9)
            var estrategia = config?.EstrategiaError ?? "Continuar";
            if (estrategia == "Cancelar")
                throw new InvalidOperationException($"Cancelado por fallo de agente {nodo.NombreAgente}: {ex.Message}");
            // "Continuar" o "Reintentar": el nodo se omite y el grafo sigue.
        }
    }

    private async Task RegistrarTrazaAsync(int idExecution, string evento, string? detalle, CancellationToken ct)
    {
        try
        {
            await _traceRepo.AddAsync(new AgentExecutionTrace
            {
                IdExecution = idExecution,
                Evento = evento,
                Detalle = detalle,
                FechaHora = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar traza {Evento}", evento);
        }
    }

    private async Task FinalizarAsync(AgentExecution execution, AgentExecutionResult result,
        bool exitoso, string? error, CancellationToken ct = default)
    {
        var fin = DateTime.UtcNow;
        execution.FechaFin = fin;
        execution.TiempoTotalMs = (long)(fin - execution.FechaInicio).TotalMilliseconds;
        execution.Estado = exitoso ? "Completado" : "Error";
        if (error != null) execution.Error = error;
        await _execRepo.UpdateAsync(execution, ct);

        result.Exitoso = exitoso;
        result.Estado = execution.Estado;
        result.TiempoTotalMs = execution.TiempoTotalMs ?? 0;
        result.CantidadAgentes = execution.CantidadAgentes;
        result.ProfundidadAlcanzada = execution.ProfundidadAlcanzada;
        if (error != null) result.Error = error;
        result.IdExecution = execution.IdExecution;

        await RegistrarTrazaAsync(execution.IdExecution, "Fin",
            exitoso ? "Ejecución completada." : $"Ejecución con error: {error}", ct);
    }
}
