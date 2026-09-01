using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
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
    private readonly ILogger<AgentOrchestrator> _logger;
    // Serializa el acceso a DbContext (no thread-safe) entre nodos del grafo.
    // El DbContext es Scoped y se comparte entre los repos; al ejecutar nodos en paralelo
    // dos hilos usaban el mismo contexto -> "second operation on this context instance".
    private readonly SemaphoreSlim _sem = new(1, 1);

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

            // 4) Ejecución por capas: paralelo dentro de capa, secuencial entre capas (Actividades 5 y 6)
            var resultadosParciales = new List<ContextoParcial>();
            var ordenWrap = new int[1]; // contador thread-safe para orden de pasos

            foreach (var capa in capas)
            {
                // Los nodos de una capa se ejecutan secuencialmente bajo el semáforo para
                // evitar concurrencia sobre el DbContext compartido (no thread-safe).
                // Entre capas se mantiene el orden secuencial del grafo.
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

                    await _sem.WaitAsync(cancellationToken);
                    try
                    {
                        var ctxAgente = await _contextManager.BuildContextForAgentAsync(
                            nodo.IdAgente, contextoGlobal, cancellationToken);
                        // ETAPA 19.3: inyectar contexto completo de pasos anteriores para que
                        // el LLM del agente tenga datos reales y no invente valores.
                        ctxAgente.ContextoPrevio = request.ContextoPrevio;
                        await EjecutarNodoAsync(execution, nodo, ctxAgente, ordenWrap, config, cancellationToken);
                    }
                    finally
                    {
                        _sem.Release();
                    }

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
            grafo.Nodos.Add(new ExecutionNode
            {
                IdNodo = idNodo++,
                IdAgente = c.IdAgente,
                NombreAgente = c.Nombre,
                Accion = $"Colaborar ({c.Rol})",
                PreguntaAsignada = c.Objetivo,
                DependeDe = c.DependeDe.Contains(principal.IdAsistente) ? new List<int> { 0 } : new List<int>()
            });
        }

        return grafo;
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
                EsEjecucionPlan = true,
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
