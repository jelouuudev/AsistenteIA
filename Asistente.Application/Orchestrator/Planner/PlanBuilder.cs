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
    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly IConexionCifrador? _cifrador;
    private readonly ISqlQueryExecutor? _executor;

    public PlanBuilder(
        IAsistenteRepository asistenteRepo,
        IOllamaService ollama,
        ILogger<PlanBuilder> logger,
        IConexionBaseDatosRepository conexionRepo,
        IWorkflowRepository workflowRepo,
        IEmbeddingProvider? embeddingProvider = null,
        IConexionCifrador? cifrador = null,
        ISqlQueryExecutor? executor = null)
    {
        _asistenteRepo = asistenteRepo;
        _ollama = ollama;
        _logger = logger;
        _conexionRepo = conexionRepo;
        _workflowRepo = workflowRepo;
        _embeddingProvider = embeddingProvider;
        _cifrador = cifrador;
        _executor = executor;
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

        var pasos = new List<PlanStep>();
        var orden = 0;

        // Catálogo de tablas autorizadas con columnas (para selección semántica:
        // el LLM mapea por significado, ej. "personal" → Empleados por sus columnas).
        var conexiones = await _conexionRepo.GetActivasAsync();
        var tablasAutorizadas = conexiones
            .SelectMany(c => c.TablasAutorizadas)
            .Select(t => t.NombreTabla)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var catalogoTablas = await DescribirTablasAsync(conexiones, tablasAutorizadas, ct);

        // Clasificación SEMÁNTICA primaria vía LLM (una sola llamada): intenciones +
        // tablas relevantes. Sin keywords: el modelo decide por significado.
        List<string> tablasMencionadas = new();
        bool quiereRag = false, quiereReporte = false, quiereRiesgo = false;
        bool quiereWorkflow = false, quiereAgregacion = false, requiereAprobacion = false;

        var semantica = await ClasificarIntencionesConLLMAsync(objetivo, catalogoTablas, tablasAutorizadas, ct);
        // Híbrido: keywords como PISO determinista (el modelo 7b varía entre runs)
        // + semántico encima. Las tablas siguen siendo solo coincidencia exacta.
        {
            var lowersKw = objetivo.ToLowerInvariant();
            tablasMencionadas = conexiones
                .SelectMany(c => c.TablasAutorizadas)
                .Where(t => ContainsWord(lowersKw, t.NombreTabla))
                .Select(t => t.NombreTabla)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            quiereRag = Contiene(lowersKw, "manual", "procedimiento", "política", "normativa", "document", "vacacion", "compar", "regla", "reglas", "permiso", "permisos");
            quiereReporte = Contiene(lowersKw, "resumen", "reporte", "informe", "ejecutivo", "pdf", "compar");
            quiereRiesgo = Contiene(lowersKw, "riesgo", "clasif", "moros");
            quiereWorkflow = Contiene(lowersKw, "flujo", "workflow", "proceso", "automatizar");
            quiereAgregacion = Contiene(lowersKw, "indicador", "indicadores", "calcular", "métrica", "métricas", "metrica", "metricas", "conteos", "totales", "agrupado", "agrupar", "cuantos", "cuántos", "cantidad", "cifra", "cifras");
            requiereAprobacion = Contiene(lowersKw, "eliminar", "elimina", "elimine", "borrar", "borra", "borre",
                "enviar", "envia", "envía", "envialo", "envíalo", "enviado", "envie", "envíe",
                "pagar", "paga", "pago", "pague", "desactivar", "desactiva",
                "publicar", "publica", "publicado", "publique",
                "aprobar", "aprueba", "apruebe", "aprobado", "autorizar", "autoriza",
                "ejecutar accion", "desplegar", "despliegue");
        }
        if (semantica != null)
        {
            // Semántico ENCIMA del piso determinista (unión: el LLM suma, nunca resta).
            quiereRag |= semantica.Rag;
            quiereReporte |= semantica.Reporte;
            quiereRiesgo |= semantica.Riesgo;
            quiereWorkflow |= semantica.Workflow;
            quiereAgregacion |= semantica.Agregacion;
            requiereAprobacion |= semantica.Aprobacion;
            _logger.LogInformation("Planner: intenciones semánticas (LLM) para '{Objetivo}': tablas=[{Tablas}] rag={Rag} reporte={Reporte} riesgo={Riesgo} wf={Wf} agr={Agr} apr={Apr}.",
                objetivo, string.Join(",", tablasMencionadas),
                quiereRag, quiereReporte, quiereRiesgo, quiereWorkflow, quiereAgregacion, requiereAprobacion);
        }
        else
        {
            // Sin LLM: queda solo el piso determinista ya calculado arriba.
            _logger.LogWarning("Planner: clasificación semántica no disponible; solo piso determinista.");
        }

        // (Unión exacta de tablas ya aplicada en el piso determinista de arriba.)

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

    /// <summary>
    /// Describe tablas autorizadas con sus columnas ("Empleados(IdEmpleado,Nombre,...)")
    /// consultando INFORMATION_SCHEMA. Si falla, devuelve solo nombres.
    /// </summary>
    private async Task<string> DescribirTablasAsync(
        IEnumerable<ConexionBaseDatos> conexiones, List<string> tablasAutorizadas, CancellationToken ct)
    {
        if (_cifrador == null || _executor == null || tablasAutorizadas.Count == 0)
            return string.Join(", ", tablasAutorizadas);

        var partes = new List<string>();
        try
        {
            foreach (var c in conexiones)
            {
                string cadena;
                try { cadena = _cifrador.Descifrar(c.CadenaConexionCifrada); }
                catch { continue; }
                foreach (var t in c.TablasAutorizadas.Select(x => x.NombreTabla).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var filas = (await _executor.ExecuteReadOnlyAsync(
                            cadena,
                            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION",
                            new Dictionary<string, object?> { ["t"] = t }, 50, ct)).ToList();
                        var cols = filas.Select(f => f.Values.FirstOrDefault()?.ToString() ?? "").Where(s => s.Length > 0);
                        partes.Add($"{t}({string.Join(",", cols)})");
                    }
                    catch { partes.Add(t); }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: no se pudo describir tablas; usando solo nombres.");
            return string.Join(", ", tablasAutorizadas);
        }
        return string.Join("; ", partes.Distinct(StringComparer.OrdinalIgnoreCase));
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
        public List<string> Tablas { get; set; } = new();
    }

    /// <summary>
    /// Clasificación SEMÁNTICA primaria vía LLM (una sola llamada): intenciones + tablas
    /// relevantes por significado, sin listas de keywords. Timeout amplio (CPU) y null
    /// ante cualquier fallo para usar el fallback determinista.
    /// </summary>
    private async Task<IntencionesRefinadas?> ClasificarIntencionesConLLMAsync(
        string objetivo, string catalogoTablas, List<string> tablasAutorizadas, CancellationToken ct)
    {
        try
        {
            var catalogo = string.IsNullOrWhiteSpace(catalogoTablas) ? "(sin tablas)" : catalogoTablas;
            var historial = new List<Mensaje>
            {
                new Mensaje
                {
                    Rol = RolMensaje.User,
                    Contenido = "Clasifica la siguiente solicitud por su SIGNIFICADO (no por palabras exactas). " +
                        "Responde SOLO este JSON, sin explicaciones: " +
                        "{\"tablas\":[\"...\"],\"rag\":bool,\"reporte\":bool,\"riesgo\":bool,\"workflow\":bool,\"agregacion\":bool,\"aprobacion\":bool}. " +
                        "Tablas disponibles en BD: [" + catalogo + "]. " +
                        "En 'tablas' lista solo las de la solicitud (nombres exactos del catálogo, [] si ninguna). " +
                        "rag=necesita documentos/políticas/manuales/reglas (ej: 'reglas de asueto' → true). " +
                        "reporte=quiere resumen/informe/pdf. " +
                        "riesgo=quiere análisis de riesgos. workflow=menciona un flujo o proceso automatizado. " +
                        "agregacion=pide cifra/cantidad/número/totales/métricas (ej: 'cifra de personal' → true). " +
                        "aprobacion=acción sensible o destructiva " +
                        "(eliminar, enviar, pagar, publicar, desactivar, aprobar algo). " +
                        "Ante la duda en rag/agregacion/reporte, prefiere true. " +
                        "Solicitud: " + objetivo
                }
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(150));
            // maxTokens amplio: los modelos reasoning (R1) consumen tokens pensando;
            // con poco tope se cortan antes del JSON.
            var respuesta = await _ollama.SendMessageAsync(historial, null, null, 0.0, 2000, cts.Token);
            _logger.LogInformation("Planner: cruda clasificación semántica: {Cruda}",
                string.IsNullOrWhiteSpace(respuesta) ? "(vacía)"
                : respuesta.Length > 500 ? respuesta[..500] + "..." : respuesta);
            if (string.IsNullOrWhiteSpace(respuesta)) return null;

            // Si hay bloque <think>, el JSON viene después.
            var finThink = respuesta.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
            var util = finThink >= 0 ? respuesta[(finThink + 8)..] : respuesta;
            var inicio = util.IndexOf('{');
            var fin = util.LastIndexOf('}');
            if (inicio < 0 || fin <= inicio) return null;

            var json = util[inicio..(fin + 1)];
            var r = System.Text.Json.JsonSerializer.Deserialize<IntencionesRefinadas>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return r;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: clasificación semántica LLM falló; se usa fallback determinista.");
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
