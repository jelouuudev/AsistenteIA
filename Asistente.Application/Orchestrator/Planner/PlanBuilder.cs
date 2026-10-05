using System;
using System.Collections.Concurrent;
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
    private readonly IDocumentoRepository? _documentoRepo;

    public PlanBuilder(
        IAsistenteRepository asistenteRepo,
        IOllamaService ollama,
        ILogger<PlanBuilder> logger,
        IConexionBaseDatosRepository conexionRepo,
        IWorkflowRepository workflowRepo,
        IEmbeddingProvider? embeddingProvider = null,
        IConexionCifrador? cifrador = null,
        ISqlQueryExecutor? executor = null,
        IDocumentoRepository? documentoRepo = null)
    {
        _asistenteRepo = asistenteRepo;
        _ollama = ollama;
        _logger = logger;
        _conexionRepo = conexionRepo;
        _workflowRepo = workflowRepo;
        _embeddingProvider = embeddingProvider;
        _cifrador = cifrador;
        _executor = executor;
        _documentoRepo = documentoRepo;
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

        // Clasificación SEMÁNTICA pura vía LLM (una sola llamada): intenciones +
        // tablas relevantes por su significado o esquema (sin listas de keywords).
        List<string> tablasMencionadas = new();
        List<string> subconsultas = new();
        bool quiereRag = false, quiereReporte = false, quiereRiesgo = false;
        bool quiereWorkflow = false, quiereAgregacion = false, requiereAprobacion = false;

        var semantica = await ClasificarIntencionesConLLMAsync(objetivo, catalogoTablas, tablasAutorizadas, ct);
        if (semantica != null)
        {
            quiereRag = semantica.Rag;
            quiereReporte = semantica.Reporte;
            quiereRiesgo = semantica.Riesgo;
            quiereWorkflow = semantica.Workflow;
            quiereAgregacion = semantica.Agregacion;
            requiereAprobacion = semantica.Aprobacion;

            if (semantica.Tablas != null && semantica.Tablas.Any())
            {
                foreach (var t in semantica.Tablas)
                {
                    var autorizada = conexiones
                        .SelectMany(c => c.TablasAutorizadas)
                        .FirstOrDefault(ta => ta.NombreTabla.Equals(t, StringComparison.OrdinalIgnoreCase));
                    if (autorizada != null && !tablasMencionadas.Contains(autorizada.NombreTabla, StringComparer.OrdinalIgnoreCase))
                    {
                        tablasMencionadas.Add(autorizada.NombreTabla);
                    }
                }

                // Validación por esquema (sin keywords): reordena las tablas del LLM
                // por coincidencia con nombres de esquema. Evita pasos nombrados con
                // la tabla equivocada (ej: "Consultar datos de Activos" para Ventas).
                if (tablasMencionadas.Count > 1)
                {
                    tablasMencionadas = tablasMencionadas
                        .OrderByDescending(n => PuntajeTabla(objetivo, n))
                        .ToList();
                }
            }

            _logger.LogInformation("Planner: intenciones semánticas (LLM) para '{Objetivo}': tablas=[{Tablas}] rag={Rag} reporte={Reporte} riesgo={Riesgo} wf={Wf} agr={Agr} apr={Apr}.",
                objetivo, string.Join(",", tablasMencionadas),
                quiereRag, quiereReporte, quiereRiesgo, quiereWorkflow, quiereAgregacion, requiereAprobacion);

            // Sub-consultas paralelas (sin keywords: las define el LLM por significado).
            // 0-1 = indivisible (un solo paso con el objetivo). Tope 4 ramas.
            if (semantica.Subconsultas != null)
            {
                subconsultas = semantica.Subconsultas
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();
                if (subconsultas.Count <= 1) subconsultas.Clear();
                else _logger.LogInformation("Planner: {N} ramas paralelas para '{Objetivo}'.", subconsultas.Count, objetivo);
            }
        }
        else
        {
            // Fallback cuando el servicio LLM no responde (sin listas rígidas de palabras clave).
            _logger.LogWarning("Planner: clasificación semántica LLM no disponible; asignando tabla por defecto si existe.");
            if (conexiones.Any(c => c.TablasAutorizadas.Any()))
            {
                var primera = conexiones.SelectMany(c => c.TablasAutorizadas).Select(t => t.NombreTabla).FirstOrDefault();
                if (!string.IsNullOrEmpty(primera)) tablasMencionadas.Add(primera);
            }
        }

        // Rescate determinista RAG por catálogo vivo (sin keywords): si el objetivo
        // menciona el código o nombre de un documento Activo registrado y el LLM no
        // marcó rag, se activa igual. "según el archivo ejemplo..." referencia al
        // documento 'ejemplo' por su código, igual que FiltroSemantico empareja
        // valores observados de BD. Sin repo o sin coincidencia, no cambia nada.
        // No toca la parte SQL: solo puede AÑADIR la rama RAG faltante.
        // Devuelve el código para que el paso RAG lo lleve como preferencia.
        var docMencionado = await DocumentoMencionadoAsync(objetivo, ct);
        if (!quiereRag && docMencionado != null)
        {
            _logger.LogInformation("Planner: rescate RAG por catálogo para '{Objetivo}': menciona un documento registrado.", objetivo);
            quiereRag = true;
        }

        // Rescate RAG SEMÁNTICO por contenido real indexado. El LLM clasificador no ve el
        // contenido de los documentos, así que no puede saber que un archivo recién
        // cargado responde la pregunta; el nombre del archivo tampoco sirve ("a").
        // Aquí el objetivo se compara contra la FIRMA REAL de cada documento Activo
        // (nombre + texto de sus fragmentos) y compite con el esquema en la misma
        // partida: gana el documento → RAG; gana el esquema → la pregunta es de
        // datos. Sin vocabulario de dominio.
        // Se calcula una sola vez porque la misma partida decide también si se
        // descarta una tabla alucinada por el LLM (puerta fuera-de-dominio) y si
        // una pregunta multi-intención merece rama RAG aunque el esquema gane.
        var (simDocumento, docGanador) = (double.NegativeInfinity, (string?)null);
        if (!quiereRag || tablasMencionadas.Any())
            (simDocumento, docGanador) = await DocumentoSemanticamenteRelacionadoAsync(objetivo, ct);

        // Rescate determinista de informe por artefacto (plan #9073): si el
        // objetivo pide el resultado en PDF ("respóndeme todo en un pdf", con o
        // sin typos) y el LLM no marcó reporte, se activa igual. Detección por
        // formato de artefacto (extensión .pdf o token "pdf"), no por vocabulario
        // de dominio: el sistema solo genera ese artefacto. Solo AÑADE el paso.
        if (!quiereReporte && PideArtefactoPdf(objetivo))
        {
            _logger.LogInformation("Planner: rescate de informe por artefacto para '{Objetivo}': pide PDF.", objetivo);
            quiereReporte = true;
        }

        // Rescate determinista SQL por esquema (sin keywords): si el LLM no devolvió
        // ninguna tabla pero el objetivo menciona una autorizada (nombre completo o
        // palabras), se usa la de mayor puntaje. Simétrico al rescate RAG: el plan no
        // depende de un solo JSON volátil del LLM (#7055 trajo solo SQL, #7056 solo
        // RAG con la misma pregunta). No toca la ejecución SQL, solo el plan.
        if (!tablasMencionadas.Any())
        {
            string? mejorTabla = null;
            var mejorPuntajeTabla = 0;
            foreach (var n in conexiones.SelectMany(c => c.TablasAutorizadas)
                .Select(t => t.NombreTabla).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var p = PuntajeTabla(objetivo, n);
                if (p > mejorPuntajeTabla) { mejorPuntajeTabla = p; mejorTabla = n; }
            }
            if (mejorTabla != null && mejorPuntajeTabla > 0)
            {
                _logger.LogInformation("Planner: rescate SQL por esquema para '{Objetivo}': tabla '{Tabla}' (puntaje {Puntaje}).", objetivo, mejorTabla, mejorPuntajeTabla);
                tablasMencionadas.Add(mejorTabla);
            }
        }

        // Si la clasificación semántica indicó consulta de datos pero no devolvió tabla física explícita,
        // se asigna la primera tabla autorizada disponible.
        if (!tablasMencionadas.Any() && quiereAgregacion && conexiones.Any(c => c.TablasAutorizadas.Any()))
        {
            var primera = conexiones.SelectMany(c => c.TablasAutorizadas).Select(t => t.NombreTabla).FirstOrDefault();
            if (!string.IsNullOrEmpty(primera))
            {
                tablasMencionadas.Add(primera);
            }
        }

        // Verificación por esquema (sin keywords): si ninguna tabla elegida empareja
        // por nombres de esquema pero otra autorizada sí, se corrige a la de mayor
        // puntaje. Evita pasos como "Consultar datos de Activos" para preguntas de
        // Ventas cuando el LLM alucina la tabla.
        if (tablasMencionadas.Any())
        {
            var todas = conexiones.SelectMany(c => c.TablasAutorizadas)
                .Select(t => t.NombreTabla).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string? mejor = null;
            var mejorPuntaje = 0;
            foreach (var n in todas)
            {
                var p = PuntajeTabla(objetivo, n);
                if (p > mejorPuntaje) { mejorPuntaje = p; mejor = n; }
            }
            var maxElegido = tablasMencionadas.Max(n => PuntajeTabla(objetivo, n));
            if (mejor != null && mejorPuntaje > 0 && maxElegido == 0)
            {
                _logger.LogWarning("Planner: tabla(s) [{Elegidas}] sin coincidencia de esquema; usando '{Mejor}' por puntaje.",
                    string.Join(",", tablasMencionadas), mejor);
                tablasMencionadas.Clear();
                tablasMencionadas.Add(mejor);
            }
        }

        // Rescate SQL por VALOR OBSERVADO, simétrico al rescate RAG por contenido
        // (plan #9109). El rescate anterior solo se disparaba si la pregunta
        // nombraba la TABLA; "los productos de marca Dell" no dice "Activos", así
        // que se quedaba sin SQL. Aquí se comparan los valores REALES (DISTINCT) de
        // todas las tablas autorizadas contra la pregunta: si aparece "Dell"
        // (Activos.Marca), esa tabla es la que responde y suelta la que inventó el
        // LLM. Sin vocabulario: los valores salen de los datos, no de una lista.
        // Si además hay una parte documental, la pregunta es mixta y conserva
        // ambas ramas (el arbitraje de abajo no descarta tablas con valor observed).
        var mapaValores = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var exactosObservados = new List<Asistente.Application.Services.Herramientas.ValorDetectado>();
        var pluralesObservados = new List<Asistente.Application.Services.Herramientas.ValorDetectado>();
        var tablasPorValor = new List<string>();
        if (_cifrador != null && _executor != null && conexiones.Any())
        {
            var todasAutorizadas = conexiones.SelectMany(c => c.TablasAutorizadas)
                .Select(t => t.NombreTabla).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (todasAutorizadas.Any())
            {
                mapaValores = await ObtenerMapaValoresAsync(conexiones, todasAutorizadas, ct);
                Asistente.Application.Services.Herramientas.FiltroSemantico.Detectar(
                    objetivo, mapaValores, exactosObservados, pluralesObservados);
                // Tope 4 columnas (no el 2 por defecto): cada columna con valor
                // real es una intención de datos y el plan ya crea un paso por
                // tabla sin tope. Recortar aquí a 2 tumbaría la tercera tabla de
                // una pregunta triple genuina.
                var porColumna = Asistente.Application.Services.Herramientas.FiltroSemantico
                    .ElegirPorColumna(exactosObservados, pluralesObservados, 4, 4);
                // El determinismo manda: si hay valores observados, la tabla que los
                // contiene es la respuesta, aunque el LLM haya nombrado otra.
                // La clave completa "Tabla.Columna" viaja en cada valor detectado: el nombre de
                // columna solo no dice de qué tabla es.
                tablasPorValor = porColumna.SelectMany(pc => pc.Value)
                    .Select(v => v.Clave.Contains('.') ? v.Clave[..v.Clave.IndexOf('.')] : string.Empty)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (tablasPorValor.Any())
                {
                    _logger.LogInformation("Planner: rescate SQL por valor observado para '{Objetivo}': tabla(s) [{Tablas}] (coinciden {Valores}).",
                        objetivo, string.Join(",", tablasPorValor),
                        string.Join(",", porColumna.SelectMany(pc => pc.Value.Select(v => $"{pc.Key}={v.Literal}"))));
                    tablasMencionadas = tablasPorValor;
                }
            }
        }

        // Rescate RAG SEMÁNTICO por contenido real indexado (va DESPUÉS del rescate
        // por valor para saber si la pregunta es multi-intención). El LLM
        // clasificador no ve el contenido de los documentos, así que no puede
        // saber que un archivo recién cargado responde la pregunta. El objetivo se
        // compara contra la FIRMA REAL de cada documento Activo y:
        //   · sin tablas SQL: basta con que el documento sea relevante;
        //   · con UNA tabla: el documento debe GANARLE al esquema por el margen
        //     (si no, una pregunta pura de datos no arrastra RAG);
        //   · con VARIAS tablas (plan #9130): la pregunta ya demostró tener varias
        //     intenciones de datos, así que basta con que el documento sea
        //     relevante: la parte documental es una intención más, independiente.
        // Sin vocabulario de dominio: todo son similitudes contra datos vivos.
        if (!quiereRag && simDocumento >= UmbralSimilitudDocumento)
        {
            var simTabla = conexiones.Any()
                ? await SimilitudMaximaTablasAsync(conexiones, conexiones.SelectMany(c => c.TablasAutorizadas)
                    .Select(t => t.NombreTabla).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), objetivo, ct)
                : double.NegativeInfinity;
            var multiIntencionDatos = tablasMencionadas.Count >= 2;
            // Pregunta de varias partes (plan #13209): "dime los pedidos que repartio
            // Marco Ruiz, y hablame sobre el Trailer de Archivo". Cada cláusula se
            // sostiene sola: una es de datos y otra de documento. Comprometerlas en
            // el mismo margen (documento − esquema ≥ 0.06) hace que gane la mitad
            // más larga y la otra desaparezca, que es justo lo que se perdia.
            // Aquí no hay Keywords: "varias partes" son las cláusulas por puntuación
            // y "el documento responde" es la sonda de contenido sobre los umbrales.
            var multiParte = DividirEnClausulas(objetivo).Count() >= 2;
            if (double.IsNegativeInfinity(simTabla)
                || simDocumento - simTabla >= MargenDocumentoSobreEsquema
                || multiIntencionDatos
                || multiParte)
            {
                _logger.LogInformation("Planner: rescate RAG semántico para '{Objetivo}': documento '{Documento}' (similitud {Sim:F3} vs esquema {SimTabla:F3}, multi-intención={Multi}).",
                    objetivo, docGanador, simDocumento, simTabla, multiIntencionDatos);
                quiereRag = true;
            }
            else
            {
                _logger.LogInformation("Planner: documento '{Documento}' similar ({Sim:F3}) pero el esquema gana ({SimTabla:F3}); no se activa RAG.",
                    docGanador, simDocumento, simTabla);
            }
        }

        // Puerta fuera-de-dominio (planes #9075, #9101, #9109 y #9157-quijote, sin
        // keywords): el LLM nombra tablas autorizadas para preguntas que no son de
        // la BD. Un umbral ABSOLUTO no sirve: astronomía puntúa 0.602 y "don
        // quijote" 0.585 contra el catálogo, dentro de la banda de las consultas
        // SQL legítimas (0.58-0.71). Lo que separa los casos no es el valor sino
        // QUIÉN GANA y si hay ANCLAJE: aquí el documento y el esquema compiten en
        // la misma partida y solo se descarta SQL cuando:
        //   · ninguna tabla elegida aparece en el texto de la pregunta,
        //   · ningún valor observado de BD empareja con la pregunta, y
        //   · gana el documento por MargenDocumentoSobreEsquema, o no hay documento
        //     y el esquema queda bajo UmbralSimilitudDominio, o el LLM nombró 3+
        //     tablas sin anclaje nominal ni de valor (patrón de alucinación: una
        //     pregunta real toca 1-2 tablas, y con 3+ valores reales el rescate ya
        //     las habría anclado). La tercera regla es INDEPENDIENTE de la
        //     similitud del documento: en una pregunta mixta el documento puede ser
        //     relevante de verdad y las tablas-sql seguir siendo inventadas (#12197).
        //     Sin vocabulario: la única señal es cuántas tablas hay y si alguna está
        //     anclada al texto o a un valor real.
        // Coincidencia nominal o de valor siempre manda sobre la similitud, así que
        // "cuántos activos...", "productos de marca Dell" y las preguntas mixtas
        // conservan su rama SQL.
        Dictionary<string, List<string>>? mapaPuerta = null;
        if (tablasMencionadas.Any() && _embeddingProvider != null && _cifrador != null && _executor != null
            && tablasMencionadas.All(n => PuntajeTabla(objetivo, n) == 0)
            && exactosObservados.Count == 0 && pluralesObservados.Count == 0)
        {
            mapaPuerta = mapaValores;
            var maxSim = await SimilitudMaximaTablasAsync(conexiones, tablasMencionadas, objetivo, ct);
            if (simDocumento >= UmbralSimilitudDocumento
                && simDocumento - maxSim >= MargenDocumentoSobreEsquema)
            {
                _logger.LogWarning("Planner: gana el documento ('{Documento}', {SimDoc:F3}) sobre el esquema ({SimTabla:F3}); descartando tablas [{Tablas}].",
                    docGanador, simDocumento, maxSim, string.Join(",", tablasMencionadas));
                tablasMencionadas.Clear();
                mapaPuerta = null;
            }
            else if (simDocumento < UmbralSimilitudDocumento && maxSim < UmbralSimilitudDominio)
            {
                _logger.LogWarning("Planner: pregunta fuera de dominio (documento {SimDoc:F3}, esquema {SimTabla:F3} < {Umbral}); descartando tablas [{Tablas}].",
                    simDocumento, maxSim, UmbralSimilitudDominio, string.Join(",", tablasMencionadas));
                tablasMencionadas.Clear();
                mapaPuerta = null;
            }
            else if (tablasMencionadas.Count >= 3)
            {
                // Esta rama NO depende de simDocumento (plan #12197): en una pregunta
                // mixta el documento puede ser relevante de verdad ("Cuerpo de Archivo"
                // está en el PDF 'ejemplo', 0.630) y aun así las tablas-sql ser
                // inventadas por el LLM para la otra mitad ("Oferta Laboral"). La
                // relevancia del documento belongs a la rama RAG y no absuelve a la
                // rama SQL. Antes esta regla exigía simDocumento < 0.60, así que un
                // documento legítimo la desactivaba y entraban 5 tablas fantasma por
                // 0.005 de margen (0.630-0.575=0.055 < 0.06 en la rama 1).
                _logger.LogWarning("Planner: {NTablas} tablas sin anclaje nominal ni de valor (documento {SimDoc:F3}, esquema {Sim:F3}); se descartan por alucinación.",
                    tablasMencionadas.Count, simDocumento, maxSim);
                tablasMencionadas.Clear();
                mapaPuerta = null;
            }
        }

        // Auto-split determinístico (sin LLM ni keywords): si UNA columna concentra
        // ≥2 valores observados que emparejan con el objetivo, cada valor es una
        // rama paralela. Vence a las subconsultas del LLM (determinista gana) y
        // rescata los casos donde el LLM no dividió (varianza del reasoning).
        if (_cifrador != null && _executor != null && tablasMencionadas.Any())
        {
            var auto = await DetectarRamasAsync(conexiones, tablasMencionadas.First(), objetivo, ct, mapaPuerta);
            if (auto.Count >= 2)
            {
                subconsultas = auto;
                _logger.LogInformation("Planner: {N} ramas deterministas para '{Objetivo}'.", auto.Count, objetivo);
            }
        }

        // Paso 0: Coordinación
        pasos.Add(Paso(ref orden, "Coordination", "Analizar la solicitud y coordinar respuesta", principal.IdAsistente,
            principal.Nombre, "El coordinador analiza el objetivo y diseña el plan de trabajo."));

        // Paso 1: Consulta(s) SQL - PUEDEN IR EN PARALELO con RAG y entre sí.
        // Si el LLM dividió el objetivo en subconsultas, cada rama es un paso Tool
        // con su Entrada propia (sub-pregunta autocontenida). Si no hay ramas pero
        // hay VARIAS tablas (plan #9130: valores observados en dos tablas), hay un
        // paso por tabla: cada uno lleva en su Entrada el contrato máquina-máquina
        // {"tabla","pregunta"} para que la herramienta no re-adivine la tabla y las
        // dos ramas consulten su base. El rol (CONSULTA) lo deriva el Orchestrator
        // del nombre de plantilla propia.
        var ordenesSql = new List<int>();
        if (tablasMencionadas.Any())
        {
            if (subconsultas.Count >= 2)
            {
                var primera = tablasMencionadas.First();
                for (int r = 0; r < subconsultas.Count; r++)
                {
                    var rama = subconsultas[r].Length > 900 ? subconsultas[r][..900] : subconsultas[r];
                    pasos.Add(Paso(ref orden, "Tool", $"Consultar datos de {primera} (SQL Server) [{r + 1}/{subconsultas.Count}]",
                        principal.IdAsistente,
                        principal.Nombre,
                        $"Ejecuta SqlQueryTool para obtener datos de {primera}.",
                        codigoHerramienta: "SqlQueryTool",
                        entrada: rama));
                    ordenesSql.Add(pasos.Count - 1);
                }
            }
            else
            {
                foreach (var tabla in tablasMencionadas)
                {
                    pasos.Add(Paso(ref orden, "Tool", $"Consultar datos de {tabla} (SQL Server)",
                        principal.IdAsistente,
                        principal.Nombre,
                        $"Ejecuta SqlQueryTool para obtener datos de {tabla}.",
                        codigoHerramienta: "SqlQueryTool",
                        entrada: EntradaSql(tabla, objetivo)));
                    ordenesSql.Add(pasos.Count - 1);
                }
            }
        }

        // Paso 2: RAG - PUEDE IR EN PARALELO con SQL (no depende de datos).
        // Si el objetivo mencionó un documento por su identificador (con o sin
        // typos), el paso lleva su código como preferencia: la recuperación lo
        // protege y lo reintenta dirigido. Sin mención no hay preferencia (un
        // documento "ganador" por similitud no la recibe: en preguntas de varios
        // documentos estrecharía la búsqueda al primero).
        if (quiereRag)
        {
            var soporte = agentes.FirstOrDefault(a => a.Codigo == "ASIS-SOP") ?? agentes.FirstOrDefault();
            pasos.Add(Paso(ref orden, "RAG", "Consultar documentación mediante RAG", soporte?.IdAsistente ?? principal.IdAsistente,
                soporte?.Nombre ?? principal.Nombre, "Recupera el procedimiento/documento desde la base de conocimiento.", codigoHerramienta: "DocumentSearchTool",
                entrada: docMencionado != null ? EntradaRag(docMencionado) : null));
        }

        // Paso 3: Workflow (B-05: solo si se resuelve un workflow activo por
        // disparadores Y asignado al agente principal; se persiste su IdWorkflow).
        // La coincidencia es SEMÁNTICA con la firma viva del workflow (medido con
        // nomic-embed-text: "ejecuta el flujo de resumen" 0.81, paráfrasis 0.60,
        // pregunta ajena de reporte 0.62). Como la paráfrasis y el falso positivo
        // caen en la misma banda, un umbral solo no los separa: hace falta DOBLE
        // SEÑAL. Regla: similitud FUERTE (>= 0.75) basta sola; en banda media
        // (>= 0.60) se exige ADEMÁS el flag workflow del LLM. Así "segun ejemplo
        // habla de la Cabecera, genera un pdf" (0.62, wf=False) no dispara un
        // workflow que nadie pidió (plan #14248: generaba un segundo PDF que
        // confundía la entrega), y la paráfrasis genuina sigue entrando.
        // Sin vocabulario: cosenos, un umbral y un flag semántico.
        var (wf, simWf) = await BuscarWorkflowPorObjetivoAsync(objetivo, principal.IdAsistente, ct);
        string? codigoWorkflow = null;
        if (wf != null)
        {
            if (simWf >= UmbralSimilitudWorkflowFuerte
                || (simWf >= UmbralSimilitudWorkflow && quiereWorkflow))
                codigoWorkflow = wf.Codigo;
            else
                _logger.LogInformation("Planner: workflow '{Wf}' similar ({Sim:F3}) pero sin señal suficiente (flag={Flag}); se omite el paso.",
                    wf.Codigo, simWf, quiereWorkflow);
        }
        if (codigoWorkflow != null)
        {
            var wfElegido = wf!;
            pasos.Add(Paso(ref orden, "Workflow", $"Ejecutar workflow: {wfElegido.Nombre}", principal.IdAsistente,
                principal.Nombre, $"Ejecuta el workflow '{wfElegido.Codigo}'.", idWorkflow: wfElegido.IdWorkflow));
        }
        else if (quiereWorkflow)
        {
            _logger.LogInformation("Planner: objetivo menciona flujo pero ningún workflow activo coincide; se omite el paso Workflow.");
        }

        // Paso 4: Análisis de indicadores (depende del SQL). Un análisis por tabla
        // cuando hay varias: cada uno agrega sobre su tabla con el mismo contrato
        // {"tabla","pregunta"} para no re-adivinar. El rol (ANALISIS) lo deriva el
        // Orchestrator del nombre de plantilla propia.
        if (tablasMencionadas.Any() && (quiereAgregacion || quiereRiesgo))
        {
            foreach (var tabla in tablasMencionadas)
            {
                var nombreAnalisis = tablasMencionadas.Count > 1
                    ? $"Analizar resultados y calcular indicadores [{tabla}]"
                    : "Analizar resultados y calcular indicadores";
                pasos.Add(Paso(ref orden, "Tool", nombreAnalisis, principal.IdAsistente,
                    principal.Nombre, "Calcula métricas sobre los datos ya consultados.", codigoHerramienta: "SqlQueryTool",
                    entrada: EntradaSql(tabla, objetivo)));
            }
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
            if (i > 0 && paso.Tipo != "RAG" && paso.Tipo != "Coordination")
            {
                plan.Dependencias.Add(new PlanDependency
                {
                    StepOrigen = 0,
                    StepDestino = paso.Orden
                });
            }

            // SQL → Análisis, Reporte, Riesgo, Entrega: desde TODAS las ramas SQL
            // (el análisis espera a que terminen las consultas en paralelo).
            if (ordenesSql.Count > 0 && !ordenesSql.Contains(i) && paso.Tipo != "Coordination"
                && (paso.Nombre.Contains("Analizar") || paso.Nombre.Contains("Generar") || paso.Nombre.Contains("Clasificar") || paso.Nombre.Contains("Entregar")))
            {
                foreach (var idxSql in ordenesSql)
                {
                    plan.Dependencias.Add(new PlanDependency
                    {
                        StepOrigen = pasos[idxSql].Orden,
                        StepDestino = paso.Orden
                    });
                }
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

            // RAG → Reporte/Entrega: consolidan también lo documental. Sin esta arista
            // corrían en la misma capa que el RAG y el informe salía sin su contenido
            // (carrera del #7056). Criterio estructural por tipo de paso.
            if ((paso.Nombre.Contains("Generar") || paso.Nombre.Contains("Entregar")) && pasos.Any(p => p.Tipo == "RAG"))
            {
                var ragPaso = pasos.First(p => p.Tipo == "RAG");
                if (ragPaso.Orden != paso.Orden)
                {
                    plan.Dependencias.Add(new PlanDependency
                    {
                        StepOrigen = ragPaso.Orden,
                        StepDestino = paso.Orden
                    });
                }
            }
        }

        plan.Pasos = pasos;
        plan.Razonamiento = await GenerarRazonamientoAsync(objetivo, pasos, ct);
        return plan;
    }

    private PlanStep Paso(ref int orden, string tipo, string nombre, int? idAsistente, string? nombreAgente, string descripcion, string? codigoHerramienta = null, int? idWorkflow = null, string? entrada = null)
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
            Entrada = entrada,
            Estado = "Pendiente"
        };
    }

    /// <summary>
    /// Contrato máquina-máquina para un paso SQL: la tabla que el plan ya resolvió
    /// (por valor observado o esquema) + la pregunta para los filtros. La
    /// herramienta lo valida contra sus autorizadas; texto plano o null en planes
    /// viejos siguen funcionando (la herramienta decide por su cuenta).
    /// </summary>
    internal static string EntradaSql(string tabla, string objetivo)
    {
        try
        {
            var pregunta = objetivo.Length > 900 ? objetivo[..900] : objetivo;
            return System.Text.Json.JsonSerializer.Serialize(new { tabla, pregunta });
        }
        catch
        {
            return objetivo;
        }
    }

    /// <summary>
    /// Contrato máquina-máquina para un paso RAG con documento mencionado: su
    /// código viaja como preferencia para la recuperación (protege sus
    /// fragmentos y habilita el reintento dirigido). Sin mención, Entrada null
    /// y la herramienta decide por similitud global.
    /// </summary>
    internal static string EntradaRag(string codigoDocumento)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(new { documento = codigoDocumento });
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// B-05: resuelve el workflow activo cuyas frases disparadoras (;) aparezcan en el
    /// objetivo Y esté asignado al agente (mundo cerrado). La coincidencia es
    /// SEMÁNTICA, no literal: se compara el objetivo contra la descripción viva
    /// del workflow (nombre + descripción + disparadores configurados) por
    /// coseno, igual que las firmas de documentos y tablas. "ejecuta el flujo
    /// de resumen" empareja con "resumen del documento" aunque no comparta las
    /// palabras exactas; y si el admin reescribe los disparadores, el
    /// comportamiento los sigue sin tocar código. Gana el mejor puntaje.
    /// Null si ninguno supera el umbral o no hay asignación.
    /// </summary>
    private async Task<(Workflow? Wf, double Sim)> BuscarWorkflowPorObjetivoAsync(string objetivo, int idAsistente, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objetivo)) return (null, double.NegativeInfinity);

        var flujos = await _workflowRepo.GetActivosAsync(ct);
        var candidatos = new List<(Workflow Wf, double Sim)>();
        if (_embeddingProvider != null)
        {
            var embObjetivos = await EmbeddingsDeConsultaAsync(objetivo, ct);
            foreach (var wf in flujos)
            {
                var firma = string.Join(" ",
                    new[] { wf.Nombre, wf.Codigo, wf.Descripcion, wf.Disparadores }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(firma)) continue;
                var embFirma = await EmbeddingCacheadoAsync(firma, ct);
                var sim = embObjetivos.Max(e => Asistente.Application.Services.Herramientas
                    .SeleccionHerramientaSemantica.Coseno(e, embFirma));
                if (sim >= UmbralSimilitudWorkflow)
                    candidatos.Add((wf, sim));
            }
        }

        foreach (var (wf, sim) in candidatos.OrderByDescending(c => c.Sim))
        {
            var agente = await _asistenteRepo.GetByIdAsync(idAsistente);
            var asignado = agente?.AgentesWorkflows.Any(aw => aw.IdWorkflow == wf.IdWorkflow && aw.Activo) ?? false;
            if (asignado) return (wf, sim);
        }

        if (candidatos.Any())
            _logger.LogInformation("Planner: hay workflows coincidentes pero ninguno asignado al agente {Asistente}; se omite el paso Workflow.", idAsistente);
        return (null, double.NegativeInfinity);
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
        /// <summary>
        /// Sub-consultas independientes en que se divide el objetivo (ramas paralelas).
        /// Cada una es autocontenida (menciona qué buscar). Vacío = indivisible.
        /// </summary>
        public List<string> Subconsultas { get; set; } = new();
    }

    /// <summary>
    /// Clasificación SEMÁNTICA primaria vía LLM (una sola llamada): intenciones + tablas
    /// relevantes por significado, sin listas de keywords. Timeout amplio (CPU) y null
    /// ante cualquier fallo para usar el fallback determinista.
    /// Caché por instancia (PlanBuilder es Scoped): evita repetir el LLM dentro de
    /// la misma petición sin congelar la clasificación entre peticiones
    /// (compartido, una respuesta vieja del LLM congelaba el ruteo 30 min y hacía
    /// que un documento recién subido no se detectara).
    /// </summary>
    private readonly ConcurrentDictionary<string, (DateTime Expira, IntencionesRefinadas Semantica)> _cacheClasificacion
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Umbral Jaro-Winkler para dar por mencionado un documento con typos
    /// ("sotenibilidad" vs "sostenibilidad..."). Medido: el par real da ~0.90;
    /// palabras comunes no relacionadas quedan muy por debajo.
    /// </summary>
    private const double UmbralMencionFuzzy = 0.85;

    /// <summary>
    /// ¿El objetivo referencia a un documento Activo registrado? Devuelve su código
    /// o null. Tres niveles, todos contra datos vivos (sin vocabulario fijo):
    /// código exacto como palabra, nombre como frase, y coincidencia tolerante a
    /// typos (Jaro-Winkler) entre cada palabra de la pregunta y los tokens del
    /// código, nombre y nombre de archivo ("sotenibilidad" → sostenibilidad_v1.pdf).
    /// Un documento nuevo o renombrado se detecta solo.
    /// </summary>
    private async Task<string?> DocumentoMencionadoAsync(string objetivo, CancellationToken ct)
    {
        if (_documentoRepo == null) return null;
        try
        {
            var objetivoNorm = Asistente.Application.Services.Herramientas.FiltroSemantico.Normalizar(objetivo);
            if (objetivoNorm.Length < 4) return null;
            var palabras = Asistente.Application.Services.Herramientas.FiltroSemantico.Palabras(objetivo);
            if (palabras.Count == 0) return null;
            var conjuntoPalabras = new HashSet<string>(palabras, StringComparer.Ordinal);
            var docs = (await _documentoRepo.GetAllAsync())
                .Where(d => d.Estado == EstadoDocumento.Activo).ToList();
            if (docs.Count == 0) return null;

            // Nivel 1 y 2: exactos (comportamiento anterior).
            foreach (var d in docs)
            {
                var codigo = Asistente.Application.Services.Herramientas.FiltroSemantico.Normalizar(d.Codigo ?? string.Empty);
                if (codigo.Length >= 4 && conjuntoPalabras.Contains(codigo)) return d.Codigo;
                var nombre = Asistente.Application.Services.Herramientas.FiltroSemantico.Normalizar(d.Nombre ?? string.Empty);
                if (nombre.Length >= 8 && objetivoNorm.Contains(nombre, StringComparison.Ordinal)) return d.Codigo;
            }

            // Nivel 3: tolerante a typos contra identificadores (código, nombre y
            // archivo). Solo tokens y palabras de 5+ letras; el umbral alto evita
            // que palabras comunes emparejen por casualidad.
            Dictionary<int, string> archivos;
            try { archivos = (await _documentoRepo.GetNombresArchivoAsync()).ToDictionary(x => x.IdDocumento, x => x.NombreArchivo); }
            catch { archivos = new Dictionary<int, string>(); }
            foreach (var d in docs)
            {
                var tokens = new List<string>();
                tokens.AddRange(Asistente.Application.Services.Herramientas.FiltroSemantico.TokensAlfanumericos(d.Codigo ?? string.Empty));
                tokens.AddRange(Asistente.Application.Services.Herramientas.FiltroSemantico.TokensAlfanumericos(d.Nombre ?? string.Empty));
                if (archivos.TryGetValue(d.IdDocumento, out var archivo))
                    tokens.AddRange(Asistente.Application.Services.Herramientas.FiltroSemantico.TokensAlfanumericos(archivo));
                if (tokens.Count == 0) continue;
                foreach (var palabra in palabras.Where(p => p.Length >= 5))
                {
                    foreach (var token in tokens)
                    {
                        if (Asistente.Application.Services.Herramientas.FiltroSemantico.JaroWinkler(palabra, token) >= UmbralMencionFuzzy)
                        {
                            _logger.LogInformation("Planner: mención tolerante a typos para '{Objetivo}': '{Palabra}' ≈ '{Token}' (documento '{Codigo}').",
                                objetivo, palabra, token, d.Codigo);
                            return d.Codigo;
                        }
                    }
                }
            }
            return null;
        }
        catch { return null; }
    }

    private async Task<IntencionesRefinadas?> ClasificarIntencionesConLLMAsync(
        string objetivo, string catalogoTablas, List<string> tablasAutorizadas, CancellationToken ct)
    {
        // La clave incluye las tablas autorizadas: la misma pregunta con distinto
        // catálogo puede clasificar distinto (y evita fugas entre tests).
        var claveCache = NormalizarObjetivo(objetivo) + "|" + string.Join(",",
            tablasAutorizadas.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
        if (_cacheClasificacion.TryGetValue(claveCache, out var cached) && cached.Expira > DateTime.UtcNow)
        {
            _logger.LogInformation("Planner: clasificación reutilizada de caché para '{Objetivo}'.", objetivo);
            return cached.Semantica;
        }

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
                        "{\"tablas\":[\"...\"],\"rag\":bool,\"reporte\":bool,\"riesgo\":bool,\"workflow\":bool,\"agregacion\":bool,\"aprobacion\":bool,\"subconsultas\":[\"...\"]}. " +
                        "Tablas disponibles en BD: [" + catalogo + "]. " +
                        "En 'tablas' lista SOLO nombres tomados del catálogo de arriba, elegidos por su significado o columnas (ej: si la consulta pide un conteo por cierta dimensión y una tabla del catálogo contiene una columna con esa dimensión, lista esa tabla). " +
                        "rag=true SOLO si pide consultar documentos, políticas, manuales, normativas o archivos de conocimiento (ej: 'resumen del documento interno' → true; 'según el documento cuáles son...' → true). " +
                        "En preguntas MIXTAS (una parte documental + otra de datos, ej: 'según el documento qué es X y cuántos registros hay...') pon rag=true Y las tablas de la parte de datos. " +
                        "Un 'reporte/informe' sobre datos de la BD NO es documental: pon rag=false y reporte=true. " +
                        "reporte=true SI pide generar, crear, exportar, producir, obtener, hacer un reporte/informe/resumen/PDF/documento (ej: 'genera un informe con los datos' → true). " +
                        "riesgo=quiere análisis de riesgos. workflow=menciona un flujo o proceso automatizado. " +
                        "agregacion=pide cifra/cantidad/número/totales/métricas/conteos (ej: 'cuántos registros hay en total' → true). " +
                        "aprobacion=acción sensible o destructiva (eliminar, enviar, pagar, publicar, desactivar, aprobar algo). " +
                        "subconsultas=divide el objetivo en consultas independientes y autocontenidas que pueden ejecutarse en paralelo " +
                        "(ej: 'compara el grupo A con el grupo B' → ['datos del grupo A', 'datos del grupo B']). " +
                        "Si es indivisible, subconsultas=[]. Cada subconsulta debe mencionar qué buscar sin depender de las otras. " +
                        "Si la solicitud es una pregunta de datos sobre la BD (conteos, cantidades, filtros), pon rag=false SALVO mención explícita de documentos, archivos, manuales o informes. " +
                        "Solicitud: " + objetivo
                }
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(150));
            // maxTokens acotado: el JSON necesita ~100 tokens; el thinking del R1
            // en CPU es el costo dominante (2000 tokens = varios minutos extra).
            var respuesta = await _ollama.SendMessageAsync(historial, null, null, 0.0, 800, cts.Token);
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
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            if (r != null)
                _cacheClasificacion[claveCache] = (DateTime.UtcNow.AddMinutes(10), r);
            return r;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: clasificación semántica LLM falló; se usa fallback determinista.");
            return null;
        }
    }

    /// <summary>
    /// Umbral de similitud pregunta↔catálogo para la puerta fuera-de-dominio.
    /// Calibrado con nomic-embed-text: pregunta ajena ("Planetas del Sistema
    /// Solar") da ~0.53 contra el catálogo; paráfrasis legítimas sin palabras de
    /// esquema ("cifra de personal") dan ~0.58. Solo decide junto a las otras dos
    /// señales (puntaje de esquema 0 y ningún valor observado).
    /// </summary>
    private const double UmbralSimilitudDominio = 0.55;

    /// <summary>
    /// Umbral de similitud pregunta↔contenido indexado para que un documento sea
    /// candidato a responderla. Calibrado con nomic-embed-text sobre los
    /// documentos reales: "dime cuales son los Planetas del Sistema Solar" puntúa
    /// 0.77 contra el manual del Sistema Solar y 0.51/0.47 contra los otros
    /// documentos; una pregunta de BD puntúa 0.51-0.59 contra el mismo manual.
    /// </summary>
    private const double UmbralSimilitudDocumento = 0.60;

    /// <summary>
    /// Umbral de similitud pregunta↔workflow para generar el paso. Mismo orden
    /// que el de documentos: el disparador es una descripción viva, no una
    /// palabra exacta, así que una paráfrasis ("ejecuta el flujo de resumen")
    /// debe emparejar con ("resumen del documento") sin compartir literales.
    /// </summary>
    private const double UmbralSimilitudWorkflow = 0.60;

    /// <summary>
    /// Similitud a partir de la cual un workflow se dispara solo, sin necesitar
    /// el flag del LLM. Medido: el pedido genuino ("ejecuta el flujo de resumen
    /// del documento ejemplo") da 0.81; una pregunta ajena de reporte ("segun
    /// ejemplo habla de la Cabecera, genera un pdf") da 0.62. El 0.75 los separa.
    /// </summary>
    private const double UmbralSimilitudWorkflowFuerte = 0.75;

    /// <summary>
    /// Margen que el documento debe ganarle al esquema para que se active RAG.
    /// Calibrado: la pregunta del Sistema Solar gana por +0.25; "activos &gt; 1000"
    /// y "ventas Electrónica" pierden por -0.19/-0.11, así que siguen yendo a SQL.
    /// </summary>
    private const double MargenDocumentoSobreEsquema = 0.06;

    /// <summary>
    /// Candidatos de comparación para una pregunta que puede tener varias partes:
    /// el objetivo entero más cada una de sus cláusulas (cortadas solo por
    /// puntuación). Sin esto, en "dime los pedidos que repartio Marco Ruiz, y
    /// hablame sobre el Trailer de Archivo" la mitad SQL domina el vector y la
    /// mitad documental se diluye (plan #13209: documento 0.631 contra esquema
    /// 0.649 → RAG descartado). Se puntúa el MEJOR de los candidatos, no la
    /// media. Sin vocabulario: solo puntuación y saltos de frase.
    /// </summary>
    private async Task<List<float[]>> EmbeddingsDeConsultaAsync(string objetivo, CancellationToken ct)
    {
        var lista = new List<float[]> { await EmbeddingCacheadoAsync(objetivo, ct) };
        foreach (var clausula in DividirEnClausulas(objetivo))
        {
            if (string.Equals(clausula, objetivo.Trim(), StringComparison.Ordinal)) continue;
            var v = await EmbeddingCacheadoAsync(clausula, ct);
            if (v.Length > 0) lista.Add(v);
        }
        return lista;
    }

    /// <summary>Cláusulas por puntuación y saltos de línea. Nunca vacío.</summary>
    internal static IEnumerable<string> DividirEnClausulas(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Array.Empty<string>();
        var partes = texto.Split(new[] { '.', ';', '!', '?', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(p => p.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);
        var lista = partes.ToList();
        return lista.Count > 0 ? lista : new[] { texto.Trim() };
    }

    /// <summary>
    /// Sonda semántica: ¿algún documento Activo con contenido indexado responde este
    /// objetivo? Compara el objetivo contra la firma real de cada documento
    /// (nombre + descripción + texto de sus fragmentos) con embeddings, sin listas
    /// de palabras: un documento recién subido se reconoce por su contenido, no
    /// por cómo se llame. Devuelve (similitud, nombre del mejor documento).
    /// </summary>
    private async Task<(double Similitud, string? Documento)> DocumentoSemanticamenteRelacionadoAsync(
        string objetivo, CancellationToken ct)
    {
        if (_documentoRepo == null || _embeddingProvider == null) return (double.NegativeInfinity, null);
        try
        {
            var contenidos = (await _documentoRepo.GetContenidosIndexadosAsync(1500)).ToList();
            if (contenidos.Count == 0) return (double.NegativeInfinity, null);

            var embObjetivos = await EmbeddingsDeConsultaAsync(objetivo, ct);
            var mejor = double.NegativeInfinity;
            string? mejorDoc = null;
            foreach (var c in contenidos)
            {
                var firma = string.Join(" ",
                    new[] { c.Nombre, c.Codigo, c.Descripcion }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    + " " + c.Texto;
                if (string.IsNullOrWhiteSpace(firma)) continue;
                var embFirma = await EmbeddingCacheadoAsync(firma, ct);
                var sim = embObjetivos.Max(e => Asistente.Application.Services.Herramientas
                    .SeleccionHerramientaSemantica.Coseno(e, embFirma));
                if (sim > mejor) { mejor = sim; mejorDoc = string.IsNullOrWhiteSpace(c.Nombre) ? c.Codigo : c.Nombre; }
            }
            _logger.LogInformation("Planner: similitud máxima pregunta↔documento {Sim:F3} ('{Documento}').", mejor, mejorDoc);
            return (mejor, mejorDoc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: no se pudo calcular la similitud con los documentos; rescate RAG semántico desactivado.");
            return (double.NegativeInfinity, null);
        }
    }

    /// <summary>
    /// Mapa de valores observados (DISTINCT real) para las tablas indicadas,
    /// clave "Tabla.Columna". Comparte la consulta con el auto-split.
    /// </summary>
    private async Task<Dictionary<string, List<string>>> ObtenerMapaValoresAsync(
        IEnumerable<ConexionBaseDatos> conexiones, List<string> tablas, CancellationToken ct)
    {
        var mapa = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var tabla in tablas)
            {
                var con = conexiones.FirstOrDefault(c =>
                    c.TablasAutorizadas.Any(t => t.NombreTabla.Equals(tabla, StringComparison.OrdinalIgnoreCase)));
                if (con == null) continue;
                var cadena = _cifrador!.Descifrar(con.CadenaConexionCifrada);

                var colsFilas = (await _executor!.ExecuteReadOnlyAsync(
                    cadena,
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t AND DATA_TYPE IN ('char','nchar','varchar','nvarchar','text','ntext') ORDER BY ORDINAL_POSITION",
                    new Dictionary<string, object?> { ["t"] = tabla }, 20, ct)).ToList();
                var columnas = colsFilas.Select(f => f.Values.FirstOrDefault()?.ToString() ?? "")
                    .Where(s => s.Length > 0).Take(10).ToList();
                if (columnas.Count == 0) continue;

                var tareas = columnas.Select(async col =>
                {
                    try
                    {
                        var colSafe = "[" + col.Replace("]", "") + "]";
                        var tabSafe = "[" + tabla.Replace("]", "") + "]";
                        var filas = (await _executor.ExecuteReadOnlyAsync(
                            cadena, $"SELECT DISTINCT TOP 20 {colSafe} AS V FROM {tabSafe} WHERE {colSafe} IS NOT NULL",
                            null, 20, ct)).ToList();
                        var vals = filas
                            .Select(f => f.TryGetValue("V", out var v) || f.TryGetValue("v", out v) ? v?.ToString() : null)
                            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
                        return (col, vals);
                    }
                    catch { return (col, new List<string>()); }
                }).ToArray();
                var resultados = await Task.WhenAll(tareas);

                foreach (var (col, vals) in resultados)
                    if (vals.Count > 0) mapa[$"{tabla}.{col}"] = vals;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: no se pudo obtener el mapa de valores; puerta fuera-de-dominio desactivada.");
        }
        return mapa;
    }

    /// <summary>
    /// Similitud máxima (coseno de embeddings) entre la pregunta y la descripción
    /// de cada tabla (nombre + TODAS sus columnas del catálogo). Sin vocabulario fijo.
    /// </summary>
    private async Task<double> SimilitudMaximaTablasAsync(
        IEnumerable<ConexionBaseDatos> conexiones, List<string> tablas, string objetivo, CancellationToken ct)
    {
        var mejor = double.NegativeInfinity;
        try
        {
            var embPreguntas = await EmbeddingsDeConsultaAsync(objetivo, ct);
            foreach (var tabla in tablas)
            {
                var columnas = await ObtenerColumnasAsync(conexiones, tabla, ct);
                var descripcion = tabla + (columnas.Count > 0 ? " " + string.Join(" ", columnas) : string.Empty);
                var embTabla = await EmbeddingCacheadoAsync(descripcion, ct);
                // Mismo criterio que el lado documento: mejor cláusula, no el
                // objetivo entero. Si no, la comparación documento↔esquema no sería
                // justa (una mitad SQL subiría el esquema y hundiría el documento).
                var sim = embPreguntas.Max(e => Asistente.Application.Services.Herramientas
                    .SeleccionHerramientaSemantica.Coseno(e, embTabla));
                if (sim > mejor) mejor = sim;
            }
            _logger.LogInformation("Planner: similitud máxima pregunta↔catálogo {Sim:F3}.", mejor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: no se pudo calcular similitud con el catálogo.");
        }
        return mejor;
    }

    /// <summary>
    /// Todas las columnas de una tabla según INFORMATION_SCHEMA (metadatos, no
    /// valores). Una sola consulta de catálogo por tabla.
    /// </summary>
    private async Task<List<string>> ObtenerColumnasAsync(
        IEnumerable<ConexionBaseDatos> conexiones, string tabla, CancellationToken ct)
    {
        try
        {
            var con = conexiones.FirstOrDefault(c =>
                c.TablasAutorizadas.Any(t => t.NombreTabla.Equals(tabla, StringComparison.OrdinalIgnoreCase)));
            if (con == null) return new();
            var cadena = _cifrador!.Descifrar(con.CadenaConexionCifrada);
            var filas = (await _executor!.ExecuteReadOnlyAsync(
                cadena,
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t ORDER BY ORDINAL_POSITION",
                new Dictionary<string, object?> { ["t"] = tabla }, 60, ct)).ToList();
            return filas.Select(f => f.Values.FirstOrDefault()?.ToString() ?? "")
                .Where(s => s.Length > 0).ToList();
        }
        catch
        {
            return new();
        }
    }

    // Caché por instancia (PlanBuilder es Scoped): evita repetir el embedding del
    // catálogo en cada pregunta sin compartir vectores entre instancias/usuarios.
    private readonly ConcurrentDictionary<string, float[]> _cacheEmbeddingsPuerta
        = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lockEmbeddingsPuerta = new(1, 1);

    private async Task<float[]> EmbeddingCacheadoAsync(string texto, CancellationToken ct)
    {
        if (_cacheEmbeddingsPuerta.TryGetValue(texto, out var e)) return e;
        await _lockEmbeddingsPuerta.WaitAsync(ct);
        try
        {
            if (_cacheEmbeddingsPuerta.TryGetValue(texto, out e)) return e;
            e = await _embeddingProvider!.GenerateEmbeddingAsync(texto).WaitAsync(ct);
            _cacheEmbeddingsPuerta[texto] = e;
            return e;
        }
        finally { _lockEmbeddingsPuerta.Release(); }
    }

    /// <summary>
    /// Ramas determinísticas: valores observados (DISTINCT real) que emparejan con
    /// el objetivo, agrupados por columna. Si una columna tiene ≥2 literales, cada
    /// uno es una rama ("Tabla con Columna Literal"). Sin LLM ni vocabulario fijo.
    /// </summary>
    private async Task<List<string>> DetectarRamasAsync(
        IEnumerable<ConexionBaseDatos> conexiones, string tabla, string objetivo, CancellationToken ct,
        Dictionary<string, List<string>>? mapaPrecalculado = null)
    {
        try
        {
            Dictionary<string, List<string>> mapa;
            if (mapaPrecalculado != null)
            {
                mapa = mapaPrecalculado
                    .Where(kv => kv.Key.StartsWith(tabla + ".", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                mapa = await ObtenerMapaValoresAsync(conexiones, new List<string> { tabla }, ct);
            }

            var exactos = new List<Asistente.Application.Services.Herramientas.ValorDetectado>();
            var plurales = new List<Asistente.Application.Services.Herramientas.ValorDetectado>();
            Asistente.Application.Services.Herramientas.FiltroSemantico.Detectar(objetivo, mapa, exactos, plurales);
            var porCol = Asistente.Application.Services.Herramientas.FiltroSemantico.ElegirPorColumna(exactos, plurales);
            foreach (var (col, vals) in porCol)
            {
                if (vals.Count >= 2)
                    return vals.Take(4).Select(v => $"{tabla} con {col} {v.Literal}").ToList();
            }
            return new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Planner: auto-split determinístico falló; se usan subconsultas del LLM.");
            return new();
        }
    }

    /// <summary>
    /// Coincidencia entre la pregunta y un nombre de tabla por esquema
    /// (nombre completo o palabras de PascalCase/snake). Sin vocabulario fijo.
    /// </summary>
    private static int PuntajeTabla(string objetivo, string nombreTabla)
    {
        if (string.IsNullOrWhiteSpace(objetivo) || string.IsNullOrWhiteSpace(nombreTabla)) return 0;
        var p = NormalizarObjetivo(objetivo);
        var n = NormalizarObjetivo(nombreTabla);
        if (p.Contains(n)) return 3;
        var palabras = System.Text.RegularExpressions.Regex.Replace(nombreTabla, @"(?<=[a-z0-9])(?=[A-Z])|[_-]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizarObjetivo)
            .Where(w => w.Length > 2);
        return palabras.Count(p.Contains);
    }

    /// <summary>
    /// ¿Pide el resultado en PDF? Detección por artefacto (extensión .pdf o token
    /// "pdf" como palabra), tolerante a typos del verbo ("repondeme todo en un
    /// pdf", plan #9073). "pdf" solo nombra el formato que el sistema genera
    /// (ReportTool); no es vocabulario de ningún dominio.
    /// </summary>
    internal static bool PideArtefactoPdf(string objetivo)
    {
        if (string.IsNullOrWhiteSpace(objetivo)) return false;
        var normalizado = NormalizarObjetivo(objetivo);
        return System.Text.RegularExpressions.Regex.IsMatch(normalizado, @"\.pdf\b|\bpdf\b");
    }

    /// <summary>Normaliza el objetivo para clave de caché (minúsculas, sin
    /// acentos, espacios colapsados). No es clasificación: solo deduplicación.</summary>
    private static string NormalizarObjetivo(string objetivo)
    {
        if (string.IsNullOrWhiteSpace(objetivo)) return string.Empty;
        var sinAcentos = new string(objetivo.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        return System.Text.RegularExpressions.Regex.Replace(sinAcentos.ToLowerInvariant().Trim(), @"\s+", " ");
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
