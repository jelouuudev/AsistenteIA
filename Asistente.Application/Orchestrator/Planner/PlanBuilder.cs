using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Plan Builder (ETAPA 18). Construye un plan con dependencias que detectan paralelismo real.
/// Pasos independientes (Tool sin depender de Agent, RAG sin depender de Tool) pueden ejecutarse en paralelo.
/// </summary>
public class PlanBuilder
{
    private readonly IAsistenteRepository _asistenteRepo;
    private readonly IOllamaService _ollama;
    private readonly ILogger<PlanBuilder> _logger;
    private readonly IConexionBaseDatosRepository _conexionRepo;
    private readonly IWorkflowRepository _workflowRepo;

    public PlanBuilder(
        IAsistenteRepository asistenteRepo,
        IOllamaService ollama,
        ILogger<PlanBuilder> logger,
        IConexionBaseDatosRepository conexionRepo,
        IWorkflowRepository workflowRepo)
    {
        _asistenteRepo = asistenteRepo;
        _ollama = ollama;
        _logger = logger;
        _conexionRepo = conexionRepo;
        _workflowRepo = workflowRepo;
    }

    public async Task<Plan> ConstruirAsync(string objetivo, int idUsuario, CancellationToken ct)
    {
        var plan = new Plan
        {
            IdUsuario = idUsuario,
            Objetivo = objetivo,
            Estado = "Borrador",
            FechaCreacion = DateTime.UtcNow,
            Version = 1
        };

        var agentes = (await _asistenteRepo.GetAllAsync()).Where(a => a.Activo).ToList();
        var principal = agentes.FirstOrDefault()
                        ?? throw new InvalidOperationException("No hay agentes disponibles para planificar.");

        var lowers = objetivo.ToLowerInvariant();
        var pasos = new List<PlanStep>();
        var orden = 0;

        // Detección dinámica de tablas
        var conexiones = await _conexionRepo.GetActivasAsync();
        var tablasMencionadas = conexiones
            .SelectMany(c => c.TablasAutorizadas)
            .Where(t => ContainsWord(lowers, t.NombreTabla))
            .Select(t => t.NombreTabla)
            .Distinct()
            .ToList();

        // Intenciones por palabras clave (vía rápida determinista)
        bool quiereRag = Contiene(lowers, "manual", "procedimiento", "política", "normativa", "document", "vacacion", "compar");
        bool quiereReporte = Contiene(lowers, "resumen", "reporte", "informe", "ejecutivo", "pdf", "compar");
        bool quiereRiesgo = Contiene(lowers, "riesgo", "clasif", "moros");
        bool quiereWorkflow = Contiene(lowers, "flujo", "workflow", "proceso", "automatizar");
        bool quiereAgregacion = Contiene(lowers, "indicador", "indicadores", "calcular", "métrica", "métricas", "metrica", "metricas", "conteos", "totales", "agrupado", "agrupar", "cuantos", "cuántos", "cantidad");
        bool requiereAprobacion = Contiene(lowers, "eliminar", "borrar", "enviar", "pagar", "desactivar", "elimina", "publicar", "publica", "aprobar", "autorizar", "ejecutar accion", "desplegar");

        // Refinamiento LLM: si NINGUNA keyword matcheó (objetivo parafraseado),
        // se pide al modelo clasificar intenciones. Con timeout corto y fallback
        // silencioso a lo determinista (sin LLM no hay timeouts ni bloqueos).
        if (!tablasMencionadas.Any() && !quiereRag && !quiereReporte && !quiereRiesgo
            && !quiereWorkflow && !quiereAgregacion && !requiereAprobacion)
        {
            var refinadas = await RefinarIntencionesConLLMAsync(objetivo, ct);
            if (refinadas != null)
            {
                quiereRag |= refinadas.Rag;
                quiereReporte |= refinadas.Reporte;
                quiereRiesgo |= refinadas.Riesgo;
                quiereWorkflow |= refinadas.Workflow;
                quiereAgregacion |= refinadas.Agregacion;
                requiereAprobacion |= refinadas.Aprobacion;
                _logger.LogInformation("Planner: intenciones refinadas por LLM para '{Objetivo}'.", objetivo);
            }
        }

        // Paso 0: Coordinación
        pasos.Add(Paso(ref orden, "Coordination", "Analizar la solicitud y coordinar respuesta", principal.IdAsistente,
            principal.Nombre, "El coordinador analiza el objetivo y diseña el plan de trabajo."));

        // Paso 1: Consulta SQL (si hay tablas) - PUEDE IR EN PARALELO con RAG
        int ordenSql = -1;
        if (tablasMencionadas.Any())
        {
            var primera = tablasMencionadas.First();
            pasos.Add(Paso(ref orden, "Tool", $"Consultar datos de {primera} (SQL Server)",
                principal.IdAsistente,
                principal.Nombre,
                $"Ejecuta SqlQueryTool para obtener datos de {primera}.",
                codigoHerramienta: "SqlQueryTool"));
            ordenSql = pasos.Count - 1;
        }

        // Paso 2: RAG - PUEDE IR EN PARALELO con SQL (no depende de datos)
        if (quiereRag)
        {
            var soporte = agentes.FirstOrDefault(a => a.Codigo == "ASIS-SOP") ?? agentes.FirstOrDefault();
            pasos.Add(Paso(ref orden, "RAG", "Consultar documentación mediante RAG", soporte?.IdAsistente ?? principal.IdAsistente,
                soporte?.Nombre ?? principal.Nombre, "Recupera el procedimiento/documento desde la base de conocimiento.", codigoHerramienta: "DocumentSearchTool"));
        }

        // Paso 3: Workflow (B-05: solo si se resuelve un workflow activo por
        // disparadores Y asignado al agente principal; se persiste su IdWorkflow)
        if (quiereWorkflow)
        {
            var wf = await BuscarWorkflowPorObjetivoAsync(objetivo, principal.IdAsistente, ct);
            if (wf != null)
            {
                pasos.Add(Paso(ref orden, "Workflow", $"Ejecutar workflow: {wf.Nombre}", principal.IdAsistente,
                    principal.Nombre, $"Ejecuta el workflow '{wf.Codigo}'.", idWorkflow: wf.IdWorkflow));
            }
            else
            {
                _logger.LogInformation("Planner: objetivo menciona flujo pero ningún workflow activo coincide; se omite el paso Workflow.");
            }
        }

        // Paso 4: Análisis de indicadores (depende del SQL)
        if (tablasMencionadas.Any() && (quiereAgregacion || quiereRiesgo))
        {
            pasos.Add(Paso(ref orden, "Tool", "Analizar resultados y calcular indicadores", principal.IdAsistente,
                principal.Nombre, "Calcula métricas automáticamente (conteos, totales) con GROUP BY.", codigoHerramienta: "SqlQueryTool"));
        }

        // Paso 5: Reporte (depende del análisis o SQL)
        if (quiereReporte)
        {
            pasos.Add(Paso(ref orden, "Tool", "Generar resumen ejecutivo / informe", principal.IdAsistente,
                principal.Nombre, "Consolida los hallazgos en un informe.", codigoHerramienta: "ReportTool"));
        }

        // Paso 6: Riesgos
        if (quiereRiesgo)
        {
            pasos.Add(Paso(ref orden, "Agent", "Clasificar por nivel de riesgo", principal.IdAsistente,
                principal.Nombre, "Evalúa y clasifica los elementos según su riesgo detectado."));
        }

        // Paso 7: Aprobación
        if (requiereAprobacion)
        {
            plan.RequiereAprobacion = true;
            pasos.Add(Paso(ref orden, "Approval", "Aprobar acción sensible antes de ejecutar", principal.IdAsistente,
                principal.Nombre, "Requiere confirmación humana."));
        }

        // Paso final: entrega
        pasos.Add(Paso(ref orden, "Agent", "Entregar resultado final al usuario", principal.IdAsistente,
            principal.Nombre, "Consolida y presenta la respuesta final."));

        // DEPENDENCIAS: Detectar paralelismo real
        // - Coordination → todos los demás
        // - SQL → Análisis, Reporte, Riesgo, Entrega
        // - RAG → Entrega (no depende de SQL)
        // - Análisis → Reporte
        for (int i = 0; i < pasos.Count; i++)
        {
            var paso = pasos[i];

            // Coordination (paso 0) → todos los demás dependen de él
            if (i > 0 && paso.Tipo != "RAG") // RAG puede ir en paralelo
            {
                plan.Dependencias.Add(new PlanDependency
                {
                    StepOrigen = 0,
                    StepDestino = paso.Orden
                });
            }

            // SQL → Análisis, Reporte, Riesgo, Entrega (pero NO Coordination)
            if (ordenSql >= 0 && paso.Orden != ordenSql && paso.Tipo != "Coordination"
                && (paso.Nombre.Contains("Analizar") || paso.Nombre.Contains("Generar") || paso.Nombre.Contains("Clasificar") || paso.Nombre.Contains("Entregar")))
            {
                plan.Dependencias.Add(new PlanDependency
                {
                    StepOrigen = pasos[ordenSql].Orden,
                    StepDestino = paso.Orden
                });
            }

            // Análisis → Reporte
            if (paso.Nombre.Contains("Generar") && pasos.Any(p => p.Nombre.Contains("Analizar")))
            {
                var analisisPaso = pasos.First(p => p.Nombre.Contains("Analizar"));
                plan.Dependencias.Add(new PlanDependency
                {
                    StepOrigen = analisisPaso.Orden,
                    StepDestino = paso.Orden
                });
            }
        }

        plan.Pasos = pasos;
        plan.Razonamiento = await GenerarRazonamientoAsync(objetivo, pasos, ct);
        return plan;
    }

    private PlanStep Paso(ref int orden, string tipo, string nombre, int? idAsistente, string? nombreAgente, string descripcion, string? codigoHerramienta = null, int? idWorkflow = null)
    {
        return new PlanStep
        {
            Orden = orden++,
            Tipo = tipo,
            Nombre = nombre,
            IdAsistente = idAsistente,
            Descripcion = descripcion,
            CodigoHerramienta = codigoHerramienta,
            IdWorkflow = idWorkflow,
            Estado = "Pendiente"
        };
    }

    /// <summary>
    /// B-05: resuelve el workflow activo cuyas frases disparadoras (;) aparezcan en el
    /// objetivo Y esté asignado al agente (mundo cerrado). Gana la frase más larga.
    /// Null si ninguno coincide o no hay asignación.
    /// </summary>
    private async Task<Workflow?> BuscarWorkflowPorObjetivoAsync(string objetivo, int idAsistente, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objetivo)) return null;

        var texto = objetivo.Trim().ToLowerInvariant();
        var flujos = await _workflowRepo.GetActivosAsync(ct);

        var candidatos = new List<(Workflow Wf, int Len)>();
        foreach (var wf in flujos)
        {
            if (string.IsNullOrWhiteSpace(wf.Disparadores)) continue;
            var mejorFrase = wf.Disparadores
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(f => texto.Contains(f.ToLowerInvariant()))
                .OrderByDescending(f => f.Length)
                .FirstOrDefault();
            if (mejorFrase != null)
                candidatos.Add((wf, mejorFrase.Length));
        }

        foreach (var (wf, _) in candidatos.OrderByDescending(c => c.Len))
        {
            var agente = await _asistenteRepo.GetByIdAsync(idAsistente);
            var asignado = agente?.AgentesWorkflows.Any(aw => aw.IdWorkflow == wf.IdWorkflow && aw.Activo) ?? false;
            if (asignado) return wf;
        }

        if (candidatos.Any())
            _logger.LogInformation("Planner: hay workflows coincidentes pero ninguno asignado al agente {Asistente}; se omite el paso Workflow.", idAsistente);
        return null;
    }

    private static bool ContainsWord(string texto, string palabra)
    {
        var palabras = palabra.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palabras.All(p => texto.Contains(p.ToLowerInvariant()));
    }

    private static bool Contiene(string texto, params string[] palabras)
        => palabras.Any(p => texto.Contains(p));

    private sealed class IntencionesRefinadas
    {
        public bool Rag { get; set; }
        public bool Reporte { get; set; }
        public bool Riesgo { get; set; }
        public bool Workflow { get; set; }
        public bool Agregacion { get; set; }
        public bool Aprobacion { get; set; }
    }

    /// <summary>
    /// Clasifica intenciones con el LLM cuando las keywords no matchearon.
    /// Devuelve null ante cualquier fallo (se conserva lo determinista).
    /// </summary>
    private async Task<IntencionesRefinadas?> RefinarIntencionesConLLMAsync(string objetivo, CancellationToken ct)
    {
        try
        {
            var historial = new List<Mensaje>
            {
                new Mensaje
                {
                    Rol = RolMensaje.User,
                    Contenido = "Clasifica el siguiente objetivo en intenciones. Responde SOLO este JSON, sin explicaciones: " +
                        "{\"rag\":bool,\"reporte\":bool,\"riesgo\":bool,\"workflow\":bool,\"agregacion\":bool,\"aprobacion\":bool}. " +
                        "rag=documentación/políticas/manuales. reporte=resumen/informe/pdf. riesgo=riesgos/morosidad. " +
                        "workflow=flujo/proceso automatizado. agregacion=conteos/totales/métricas. aprobacion=acción sensible " +
                        "(eliminar/enviar/pagar). Objetivo: " + objetivo
                }
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(20));
            var respuesta = await _ollama.SendMessageAsync(historial, cts.Token);
            if (string.IsNullOrWhiteSpace(respuesta)) return null;

            var inicio = respuesta.IndexOf('{');
            var fin = respuesta.LastIndexOf('}');
            if (inicio < 0 || fin <= inicio) return null;

            var json = respuesta[inicio..(fin + 1)];
            var r = System.Text.Json.JsonSerializer.Deserialize<IntencionesRefinadas>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return r;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: refinamiento LLM de intenciones falló; se conserva lo determinista.");
            return null;
        }
    }

    private async Task<string> GenerarRazonamientoAsync(string objetivo, List<PlanStep> pasos, CancellationToken ct)
    {
        try
        {
            var historial = new List<Mensaje>
            {
                new Mensaje { Rol = RolMensaje.User, Contenido = $"Objetivo: {objetivo}. Pasos: {string.Join("; ", pasos.Select(p => p.Nombre))}." }
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            return await _ollama.SendMessageAsync(historial, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: razonamiento LLM omitido (Ollama no disponible en CPU).");
            return $"Plan generado por el Plan Builder basado en intención. {pasos.Count} paso(s) estructurados como DAG.";
        }
    }
}
