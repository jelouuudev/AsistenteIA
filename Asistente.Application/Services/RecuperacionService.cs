using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class RecuperacionService : IRecuperacionService
{
    private readonly IProcesamientoDocumentalRepository _procesamientoRepository;
    private readonly IVectorStore _vectorStore;
    private readonly EmbeddingService _embeddingService;
    private readonly IAsistenteFuenteRepository _asistenteFuenteRepository;
    private readonly IDocumentoFuenteRepository _documentoFuenteRepository;
    private readonly IFuenteConocimientoRepository _fuenteRepository;
    private readonly IConfiguracionRAGRepository _configuracionRAGRepository;
    private readonly ILogger<RecuperacionService> _logger;

    private const int MaxChunksDefault = 5;
    private const int MaxCaracteresContextoDefault = 12000;
    private const float MinScoreDefault = 0.7f;

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Español
        "el","la","los","las","un","una","unos","unas","de","del","al","a","en","por","para",
        "con","sin","sobre","entre","desde","hasta","que","qué","cómo","cuál","dónde","quién",
        "y","o","pero","si","no","muy","más","menos","tan","tanto","este","esta","esto","ese",
        "esa","eso","aquel","aquella","aquello","me","te","se","lo","le","nos","les","su","mis",
        "tu","sus","como","cómo","cuando","cuándo","porque","puedo","puede","puedes",
        "hacer","tiene","tienes","tengo","hay","son","es","está","están","fue","fueron","ser",
        "estar","haber","todo","toda","todos","todas","uno","dos","tres","cual","cuales",
        "usted","ellos","ellas","nosotros","sistema","los","las","fue","son","parte","tambien",
        // Inglés
        "the","and","for","with","you","your","are","was","were","that","this","from","into"
    };

    public RecuperacionService(
        IProcesamientoDocumentalRepository procesamientoRepository,
        IVectorStore vectorStore,
        EmbeddingService embeddingService,
        IAsistenteFuenteRepository asistenteFuenteRepository,
        IDocumentoFuenteRepository documentoFuenteRepository,
        IFuenteConocimientoRepository fuenteRepository,
        IConfiguracionRAGRepository configuracionRAGRepository,
        ILogger<RecuperacionService> logger)
    {
        _procesamientoRepository = procesamientoRepository;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _asistenteFuenteRepository = asistenteFuenteRepository;
        _documentoFuenteRepository = documentoFuenteRepository;
        _fuenteRepository = fuenteRepository;
        _configuracionRAGRepository = configuracionRAGRepository;
        _logger = logger;
    }

    public async Task<string> RecuperarContextoAsync(string pregunta, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
            return string.Empty;

        var config = await _configuracionRAGRepository.GetActivaAsync();
        var maxChunks = config?.MaxChunks ?? MaxChunksDefault;
        var maxCaracteres = config?.MaxCaracteresContexto ?? MaxCaracteresContextoDefault;
        var minScore = (float)(config?.MinScore ?? MinScoreDefault);
        // Tope de fragmentos al modelo (antes no se leia: parametro muerto).
        var maxChunksAlModeloSinFuentes = Math.Max(1, config?.MaxChunksAlModelo ?? 10);
        maxChunks = Math.Min(maxChunks, maxChunksAlModeloSinFuentes);

        try
        {
            var documentoFiltrado = await DetectarDocumentoAsync(pregunta);
            IEnumerable<VectorSearchResult> resultadosSemanticos;

            if (documentoFiltrado != null)
            {
                _logger.LogInformation("Documento detectado en pregunta: '{DocumentName}'. Buscando solo en ese documento.", documentoFiltrado);
                resultadosSemanticos = await _vectorStore.SearchByDocumentAsync(pregunta, maxChunks, documentoFiltrado);
            }
            else
            {
                resultadosSemanticos = await _vectorStore.SearchAsync(pregunta, maxChunks * 2);
            }
            
            if (resultadosSemanticos != null && resultadosSemanticos.Any())
            {
                var filtrados = resultadosSemanticos.Where(r => r.Score >= minScore).Take(maxChunks).ToList();
                
                if (filtrados.Count == 0)
                {
                    _logger.LogInformation("Recuperacion semantica: {Cantidad} chunks encontrados pero ninguno supera score minimo ({MinScore:F2}). Mejor score: {BestScore:F2}. Pregunta: {Pregunta}",
                        resultadosSemanticos.Count(), minScore, resultadosSemanticos.Max(r => r.Score), pregunta);
                }
                else
                {
                    _logger.LogInformation("Recuperacion semantica: {Cantidad} chunks filtrados de {Total} encontrados (score >= {MinScore:F2}). Pregunta: {Pregunta}",
                        filtrados.Count, resultadosSemanticos.Count(), minScore, pregunta);
                }
                
                resultadosSemanticos = filtrados;
                
                var sb = new StringBuilder();
                var caracteres = 0;
                
                foreach (var resultado in resultadosSemanticos)
                {
                    var texto = resultado.Text.Trim();
                    if (caracteres + texto.Length > maxCaracteres)
                    {
                        var restante = maxCaracteres - caracteres;
                        if (restante > 200)
                        {
                            sb.AppendLine(texto.Substring(0, restante).Trim());
                            sb.AppendLine("\n---");
                        }
                        break;
                    }

                    sb.AppendLine(texto);
                    sb.AppendLine("\n---");
                    caracteres += texto.Length;
                    
                    _logger.LogInformation("Chunk semantico (Score: {Score:0.00}): {Texto}", 
                        resultado.Score,
                        texto.Length > 200 ? texto.Substring(0, 200) + "..." : texto);
                }
                
                if (caracteres > 0)
                    return sb.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Busqueda semantica fallo, usando busqueda lexica como fallback.");
        }

        return await RecuperarContextoLexicoAsync(pregunta, maxChunks, maxCaracteres, cancellationToken);
    }

    private async Task<string> RecuperarContextoLexicoAsync(string pregunta, int maxChunks, int maxCaracteres, CancellationToken cancellationToken)
    {
        var documentos = (await _procesamientoRepository.GetAllAsync())
            .Where(d => d.Estado == EstadoProcesamiento.Procesado && d.Chunks != null && d.Chunks.Count > 0)
            .ToList();

        var chunks = documentos.SelectMany(d => d.Chunks).ToList();

        if (chunks.Count == 0)
        {
            _logger.LogInformation("Recuperación léxica: no hay chunks procesados para consultar.");
            return string.Empty;
        }

        var preguntaNormalizada = Normalizar(pregunta);
        var terminosPregunta = Tokenizar(preguntaNormalizada);

        if (terminosPregunta.Count == 0)
            return string.Empty;

        var puntuados = PuntuarLexico(preguntaNormalizada, terminosPregunta, chunks, cancellationToken);

        if (puntuados.Count == 0)
        {
            _logger.LogInformation("Recuperación léxica: ningún chunk relevante para la consulta.");
            return string.Empty;
        }

        var mejores = puntuados
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.chunk.Orden)
            .Take(maxChunks)
            .Select(x => x.chunk)
            .ToList();

        var sb = new StringBuilder();
        var caracteres = 0;
        foreach (var chunk in mejores)
        {
            var texto = chunk.Texto.Trim();
            if (caracteres + texto.Length > maxCaracteres)
            {
                var restante = maxCaracteres - caracteres;
                if (restante > 200)
                {
                    sb.AppendLine(texto.Substring(0, restante).Trim());
                    sb.AppendLine("\n---");
                }
                break;
            }

            sb.AppendLine(texto);
            sb.AppendLine("\n---");
            caracteres += texto.Length;
        }

        _logger.LogInformation("Recuperación léxica: {Cantidad} chunks seleccionados de {Total}. Pregunta: {Pregunta}", 
            mejores.Count, chunks.Count, pregunta);

        return sb.ToString().Trim();
    }

    private static List<(DocumentoChunk chunk, double score)> PuntuarLexico(
        string preguntaNormalizada,
        List<string> terminosPregunta,
        IEnumerable<DocumentoChunk> chunks,
        CancellationToken cancellationToken)
    {
        var puntuados = new List<(DocumentoChunk chunk, double score)>();

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var textoNormalizado = Normalizar(chunk.Texto);
            var frecuencias = Frecuencias(textoNormalizado);

            double score = 0;

            // Scoring por frecuencia de términos (TF)
            foreach (var termino in terminosPregunta)
            {
                if (frecuencias.TryGetValue(termino, out var f))
                    score += 1 + Math.Log(1 + f);
            }

            // Boost por coincidencia exacta de la pregunta completa
            if (textoNormalizado.Contains(preguntaNormalizada))
                score += 15;

            // Detección de títulos y secciones (boost fuerte)
            var lineas = textoNormalizado.Split('\n');
            var primeraLinea = lineas.Length > 0 ? lineas[0] : "";

            // Si la primera línea parece un título (corta y contiene términos de la pregunta)
            if (primeraLinea.Length < 100)
            {
                var coincidenciasTitulo = terminosPregunta.Count(t => primeraLinea.Contains(t));
                if (coincidenciasTitulo > 0)
                    score += coincidenciasTitulo * 5;
            }

            // Boost por términos al inicio del chunk (primeras 5 líneas)
            var primerasLineas = string.Join(" ", lineas.Take(5));
            foreach (var termino in terminosPregunta)
            {
                if (primerasLineas.Contains(termino))
                    score += 3;
            }

            // Boost por encabezado de sección en cualquier parte del chunk
            var mejorEncabezado = 0;
            foreach (var linea in lineas)
            {
                var coincidencias = CoincidenciasEncabezado(linea, terminosPregunta);
                if (coincidencias > mejorEncabezado)
                    mejorEncabezado = coincidencias;
            }
            if (mejorEncabezado >= 2)
                score += mejorEncabezado * 8;

            // Boost por múltiples términos coincidentes (densidad)
            var terminosCoincidentes = terminosPregunta.Count(t => frecuencias.ContainsKey(t));
            var densidad = terminosPregunta.Count > 0 ? (double)terminosCoincidentes / terminosPregunta.Count : 0;
            score += densidad * 5;

            // Boost por términos específicos de alta relevancia
            var terminosClave = new[] { "solucion", "problema", "error", "configuracion", "instalacion", "requisito", "objetivo", "horario", "politica", "contraseña", "base de datos", "modelo" };
            foreach (var termino in terminosClave)
            {
                if (terminosPregunta.Contains(termino) && frecuencias.ContainsKey(termino))
                    score += 10;

                if (terminosPregunta.Contains(termino) && primerasLineas.Contains(termino))
                    score += 8;
            }

            // Boost por frase completa "solución de problemas"
            if (preguntaNormalizada.Contains("solucion") && preguntaNormalizada.Contains("problema"))
            {
                if (textoNormalizado.Contains("solucion") && textoNormalizado.Contains("problema"))
                    score += 15;
            }

            if (score > 0)
                puntuados.Add((chunk, score));
        }

        return puntuados;
    }

    private static string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var sinAcentos = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in sinAcentos)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static List<string> Tokenizar(string texto)
    {
        var tokens = new List<string>();
        var separadores = new[]
        {
            ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '¿', '?', '¡', '(', ')', '"', '\'',
            '/', '\\', '-', '_', '*', '+', '='
        };

        foreach (var parte in texto.Split(separadores, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = parte.Trim();
            if (t.Length < 3 || Stopwords.Contains(t))
                continue;
            tokens.Add(Stemizar(t));
        }

        return tokens;
    }

    private static string Stemizar(string token)
    {
        if (token.Length < 5)
            return token;

        if (token.EndsWith("ciones"))
            return token.Substring(0, token.Length - 2);

        if (token.EndsWith("es"))
            return token.Substring(0, token.Length - 2);

        if (token.EndsWith("s"))
            return token.Substring(0, token.Length - 1);

        return token;
    }

    private static int CoincidenciasEncabezado(string linea, List<string> terminosPregunta)
    {
        var limpiada = linea.Trim();
        if (limpiada.Length < 3 || limpiada.Length > 200)
            return 0;

        foreach (Match m in Regex.Matches(limpiada, @"\d+(\.\d+)*\.?\s"))
        {
            var candidato = limpiada.Substring(m.Index, Math.Min(150, limpiada.Length - m.Index))
                .TrimStart('.', ' ', '-', ':');
            var coincidencias = terminosPregunta.Count(t => candidato.Contains(t));
            if (coincidencias >= 2)
                return coincidencias;
        }

        return 0;
    }

    private static Dictionary<string, int> Frecuencias(string texto)
    {
        var frecuencias = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var termino in Tokenizar(texto))
        {
            frecuencias.TryGetValue(termino, out var f);
            frecuencias[termino] = f + 1;
        }
        return frecuencias;
    }

    public async Task<(string Contexto, List<ReferenciaDocumentalDto> Referencias)> RecuperarContextoConFuentesAsync(string pregunta, int? idAsistente = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
            return (string.Empty, new List<ReferenciaDocumentalDto>());

        var config = await _configuracionRAGRepository.GetActivaAsync();
        var maxChunks = config?.MaxChunks ?? MaxChunksDefault;
        var maxCaracteres = config?.MaxCaracteresContexto ?? MaxCaracteresContextoDefault;
        var minScore = (float)(config?.MinScore ?? MinScoreDefault);
        // Parametros de contexto: antes se guardaban pero ningun servicio los leia.
        var maxChunksAlModelo = Math.Max(1, config?.MaxChunksAlModelo ?? 10);
        var maxReferencias = Math.Max(1, config?.MaxReferencias ?? 10);
        _logger.LogInformation("Config RAG contexto: MaxChunks={MaxChunks}, MaxChunksAlModelo={MaxChunksAlModelo}, MaxReferencias={MaxReferencias}, MaxCaracteres={MaxCaracteres}.",
            maxChunks, maxChunksAlModelo, maxReferencias, maxCaracteres);

        VectorSearchFilter? filtro = null;
        Dictionary<int, string>? nombresFuentes = null;
        Dictionary<int, int>? prioridadesFuentes = null;
        var documentosProcesadosIds = new List<int>();

        if (idAsistente.HasValue)
        {
            var fuentesAutorizadas = await _asistenteFuenteRepository.GetFuentesActivasPorAsistenteAsync(idAsistente.Value);
            if (!fuentesAutorizadas.Any())
            {
                _logger.LogInformation("El asistente {IdAsistente} no tiene fuentes autorizadas activas.", idAsistente.Value);
                return (string.Empty, new List<ReferenciaDocumentalDto>());
            }

            // Tope de fuentes simultaneas (antes no se leia: parametro muerto).
            // Se consultan las de mayor Prioridad primero.
            var maxFuentes = Math.Max(1, config?.MaxFuentesConsultadas ?? 5);
            var fuentesAConsultar = fuentesAutorizadas
                .OrderByDescending(f => f.Prioridad)
                .Take(maxFuentes)
                .ToList();
            if (fuentesAConsultar.Count < fuentesAutorizadas.Count())
                _logger.LogInformation("Fuentes recortadas: {Total} -> {Tope} (MaxFuentesConsultadas), por prioridad.",
                    fuentesAutorizadas.Count(), fuentesAConsultar.Count);

            // Documentos historicos/obsoletos segun configuracion (antes hardcoded false).
            var incluirHistoricos = config?.UsarDocumentosHistoricos ?? false;

            var idsFuentes = fuentesAConsultar.Select(f => f.IdFuente).ToList();
            nombresFuentes = fuentesAConsultar.ToDictionary(f => f.IdFuente, f => f.Nombre);
            prioridadesFuentes = fuentesAConsultar.ToDictionary(f => f.IdFuente, f => f.Prioridad);

            foreach (var idFuente in idsFuentes)
            {
                var ids = await _documentoFuenteRepository.GetDocumentosProcesadosIdsByFuenteAsync(idFuente);
                documentosProcesadosIds.AddRange(ids);
            }

            filtro = new VectorSearchFilter
            {
                IdsFuentes = idsFuentes,
                IncluirHistoricos = incluirHistoricos
            };

            _logger.LogInformation("Asistente {IdAsistente}: {CountFuentes} fuentes autorizadas, {CountDocs} documentos procesados disponibles. IncluirHistoricos={IncluirHistoricos}.",
                idAsistente.Value, idsFuentes.Count, documentosProcesadosIds.Count, incluirHistoricos);
        }

        try
        {
            IEnumerable<VectorSearchResult> resultadosSemanticos;

            if (filtro != null)
            {
                var resultadosList = new List<VectorSearchResult>();
                var topKPorFuente = config?.TopKPorFuente ?? 5;

                if (filtro.IdsFuentes != null && filtro.IdsFuentes.Any())
                {
                    foreach (var idFuente in filtro.IdsFuentes)
                    {
                        var filtroIndividual = new VectorSearchFilter
                        {
                            IdsFuentes = new List<int> { idFuente },
                            IncluirHistoricos = filtro.IncluirHistoricos,
                            IdsDocumentos = filtro.IdsDocumentos,
                            IdCategoria = filtro.IdCategoria,
                            IdVersion = filtro.IdVersion,
                            EstadoDocumento = filtro.EstadoDocumento
                        };

                        var resFuente = await _vectorStore.SearchWithFilterAsync(pregunta, topKPorFuente, filtroIndividual);
                        if (resFuente != null)
                        {
                            resultadosList.AddRange(resFuente);
                        }
                    }
                    resultadosSemanticos = resultadosList
                        .GroupBy(r => new { r.DocumentoProcesadoId, r.Orden })
                        .Select(g => g.First())
                        .ToList();
                }
                else
                {
                    resultadosSemanticos = await _vectorStore.SearchWithFilterAsync(pregunta, maxChunks * 3, filtro);
                }
                
                // SEGURIDAD (fix desasignacion): si el filtro por fuentes no devolvio
                // resultados, NO se hace fallback sin filtro. El fallback exponia
                // documentos de fuentes no asignadas al asistente.
                if (!resultadosSemanticos.Any())
                {
                    _logger.LogWarning("Filtro por fuentes no devolvio resultados para la pregunta. Sin fallback sin filtro por seguridad.");
                    resultadosSemanticos = Enumerable.Empty<VectorSearchResult>();
                }
                else if (documentosProcesadosIds.Count > 0)
                {
                    // SEGURIDAD: solo documentos asignados a las fuentes del asistente.
                    // Los vectores en Chroma pueden conservar metadata de una fuente
                    // anterior (huerfanos tras desasignar); la lista autorizada desde
                    // SQL es la que manda.
                    var autorizados = new HashSet<int>(documentosProcesadosIds.Distinct());
                    var totalAntes = resultadosSemanticos.Count();
                    resultadosSemanticos = resultadosSemanticos
                        .Where(r => autorizados.Contains(r.DocumentoProcesadoId))
                        .ToList();
                    var totalDespues = resultadosSemanticos.Count();
                    if (totalDespues < totalAntes)
                        _logger.LogInformation("Filtro de asignacion documento-fuente excluyo {Excluidos} resultado(s) no autorizado(s).", totalAntes - totalDespues);
                }
                else
                {
                    // El asistente tiene fuentes pero ningun documento asignado a ellas.
                    resultadosSemanticos = Enumerable.Empty<VectorSearchResult>();
                }
            }
            else
            {
                resultadosSemanticos = await _vectorStore.SearchAsync(pregunta, maxChunks * 2);
            }

            if (resultadosSemanticos != null && resultadosSemanticos.Any())
            {
                var listaResultados = resultadosSemanticos
                    .Where(r => Math.Clamp(r.Score, 0f, 1f) >= minScore)
                    .ToList();

                if (prioridadesFuentes != null && prioridadesFuentes.Count > 0)
                {
                    int maxPrioridad = prioridadesFuentes.Values.Max();
                    listaResultados = AplicarPrioridadARanking(listaResultados, prioridadesFuentes, maxPrioridad);
                }

                var filtrados = listaResultados.Take(maxChunks).ToList();

                if (documentosProcesadosIds.Count > 0 && filtrados.Count < maxChunks * 2)
                {
                    var chunksAutorizados = (await _procesamientoRepository.GetAllAsync())
                        .Where(d => documentosProcesadosIds.Contains(d.IdDocumentoProcesado)
                                    && d.Estado == EstadoProcesamiento.Procesado
                                    && d.Chunks != null && d.Chunks.Count > 0)
                        .SelectMany(d => d.Chunks)
                        .ToList();

                    var preguntaNormalizada = Normalizar(pregunta);
                    var terminosPregunta = Tokenizar(preguntaNormalizada);

                    if (terminosPregunta.Count > 0)
                    {
                        var lexicos = PuntuarLexico(preguntaNormalizada, terminosPregunta, chunksAutorizados, cancellationToken)
                            .Where(l => l.score > 0)
                            .OrderByDescending(l => l.score)
                            .ThenBy(l => l.chunk.Orden);

                        var usados = new HashSet<(int, int)>(filtrados.Select(r => (r.DocumentoProcesadoId, r.Orden)));

                        foreach (var (chunk, score) in lexicos)
                        {
                            if (filtrados.Count >= maxChunks * 2)
                                break;

                            // FIX MinScore: el relleno lexico tambien debe respetar el umbral
                            // (misma escala 0-1 que el coseno semantico).
                            var scoreAlmacenado = (float)Math.Min(1.0, score / 20.0);
                            var scoreNormalizadoLex = Math.Clamp(scoreAlmacenado, 0f, 1f);
                            if (scoreNormalizadoLex < minScore)
                                continue;

                            var clave = (chunk.IdDocumentoProcesado, chunk.Orden);
                            if (usados.Contains(clave))
                            {
                                var existente = filtrados.FirstOrDefault(r =>
                                    r.DocumentoProcesadoId == clave.Item1 && r.Orden == clave.Item2);
                                if (existente != null)
                                {
                                    if (scoreAlmacenado > existente.Score)
                                        existente.Score = scoreAlmacenado;
                                }
                                continue;
                            }

                            var nombreDoc = chunk.DocumentoProcesado?.VersionDocumento?.Documento?.Nombre ?? "Documento";

                            filtrados.Add(new VectorSearchResult
                            {
                                DocumentoProcesadoId = chunk.IdDocumentoProcesado,
                                ChunkId = chunk.IdChunk,
                                Text = chunk.Texto,
                                Score = scoreAlmacenado,
                                Orden = chunk.Orden,
                                PaginaInicial = chunk.PaginaInicial,
                                PaginaFinal = chunk.PaginaFinal,
                                MetadataDocumentoNombre = nombreDoc
                            });
                            usados.Add(clave);
                        }

                        var expandidos = new HashSet<(int, int)>(usados);
                        foreach (var resultado in filtrados.ToList())
                        {
                            if (filtrados.Count >= maxChunks * 3)
                                break;

                            var textoNorm = Normalizar(resultado.Text);
                            var tieneEncabezado = textoNorm
                                .Split('\n')
                                .Any(linea => CoincidenciasEncabezado(linea, terminosPregunta) >= 2);

                            if (!tieneEncabezado)
                                continue;

                            foreach (var delta in new[] { 1, -1 })
                            {
                                if (filtrados.Count >= maxChunks * 3)
                                    break;

                                var vecino = chunksAutorizados.FirstOrDefault(c =>
                                    c.IdDocumentoProcesado == resultado.DocumentoProcesadoId
                                    && c.Orden == resultado.Orden + delta
                                    && !expandidos.Contains((c.IdDocumentoProcesado, c.Orden)));

                                if (vecino == null)
                                    continue;

                                var nombreVecino = vecino.DocumentoProcesado?.VersionDocumento?.Documento?.Nombre ?? "Documento";
                                filtrados.Add(new VectorSearchResult
                                {
                                    DocumentoProcesadoId = vecino.IdDocumentoProcesado,
                                    ChunkId = vecino.IdChunk,
                                    Text = vecino.Texto,
                                    Score = resultado.Score,
                                    Orden = vecino.Orden,
                                    PaginaInicial = vecino.PaginaInicial,
                                    PaginaFinal = vecino.PaginaFinal,
                                    MetadataDocumentoNombre = nombreVecino
                                });
                                expandidos.Add((vecino.IdDocumentoProcesado, vecino.Orden));
                            }
                        }
                    }
                }

                if (filtrados.Count > 0)
                {
                    // Seguridad: ningun chunk (semantico, lexico o vecino expandido)
                    // puede quedar por debajo del MinScore configurado.
                    // Tope de fragmentos al modelo (MaxChunksAlModelo).
                    filtrados = filtrados
                        .Where(r => Math.Clamp(r.Score, 0f, 1f) >= minScore)
                        .OrderByDescending(r => r.Score)
                        .Take(maxChunksAlModelo)
                        .ToList();
                    _logger.LogInformation("Contexto al modelo: {Count} chunks (tope MaxChunksAlModelo={Tope}).", filtrados.Count, maxChunksAlModelo);

                    var sb = new StringBuilder();
                    var caracteres = 0;
                    var referencias = new List<ReferenciaDocumentalDto>();

                    foreach (var resultado in filtrados)
                    {
                        var texto = resultado.Text.Trim();
                        if (caracteres + texto.Length > maxCaracteres)
                        {
                            var restante = maxCaracteres - caracteres;
                            if (restante > 200)
                            {
                                sb.AppendLine(texto.Substring(0, restante).Trim());
                                sb.AppendLine("\n---");
                            }
                            break;
                        }

                        sb.AppendLine(texto);
                        sb.AppendLine("\n---");
                        caracteres += texto.Length;

                        var nombreFuente = "";
                        if (nombresFuentes != null && resultado.IdFuente.HasValue && nombresFuentes.TryGetValue(resultado.IdFuente.Value, out var nf))
                            nombreFuente = nf;

                        var scoreNormalizado = Math.Clamp(resultado.Score, 0f, 1f);

                        referencias.Add(new ReferenciaDocumentalDto
                        {
                            NombreDocumento = resultado.MetadataDocumentoNombre ?? "Documento",
                            NombreFuente = nombreFuente,
                            VersionDocumento = resultado.VersionDocumento,
                            PaginaInicial = resultado.PaginaInicial,
                            PaginaFinal = resultado.PaginaFinal,
                            FragmentoUtilizado = texto.Length > 200 ? texto.Substring(0, 200) + "..." : texto,
                            PuntajeSimilitud = scoreNormalizado
                        });

                        _logger.LogInformation("Chunk recuperado (Score: {Score:0.00}): {Texto}",
                            resultado.Score,
                            texto.Length > 200 ? texto.Substring(0, 200) + "..." : texto);
                    }

                    // Tope de referencias mostradas al usuario (MaxReferencias).
                    if (referencias.Count > maxReferencias)
                    {
                        _logger.LogInformation("Referencias recortadas: {Total} -> {Tope} (MaxReferencias).", referencias.Count, maxReferencias);
                        referencias = referencias.Take(maxReferencias).ToList();
                    }

                    return (sb.ToString().Trim(), referencias);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Busqueda semantica con fuentes fallo.");
        }

        return (string.Empty, new List<ReferenciaDocumentalDto>());
    }

    private List<VectorSearchResult> AplicarPrioridadARanking(
        List<VectorSearchResult> resultados,
        Dictionary<int, int> prioridadesFuentes,
        int maxPrioridad)
    {
        var ponderados = resultados.Select(r =>
        {
            double factorPrioridad = 1.0;
            if (maxPrioridad > 0)
            {
                int prioridad = maxPrioridad;
                if (r.IdFuente.HasValue && prioridadesFuentes.TryGetValue(r.IdFuente.Value, out var p))
                    prioridad = p;

                factorPrioridad = (double)prioridad / maxPrioridad;
            }

            double scorePonderado = r.Score * factorPrioridad;

            return new { Resultado = r, ScorePonderado = scorePonderado };
        })
        .OrderByDescending(x => x.ScorePonderado)
        .Select(x =>
        {
            var r = x.Resultado;
            r.Score = (float)x.ScorePonderado;
            return r;
        })
        .ToList();

        _logger.LogInformation("Ranking ponderado por prioridad: {Count} resultados. Top score: {TopScore:F4}",
            ponderados.Count, ponderados.FirstOrDefault()?.Score ?? 0f);

        return ponderados;
    }

    private async Task<string?> DetectarDocumentoAsync(string pregunta)
    {
        try
        {
            var documentNames = await _vectorStore.GetAllDocumentNamesAsync();
            var documentCounts = await _vectorStore.GetDocumentCountsAsync();
            var preguntaLower = Normalizar(pregunta);

            var palabrasPregunta = preguntaLower.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            var indicadores = new[] { "segun", "segundo", "conforme", "acuerdo" };
            var indicadorPrefijos = new[] { "del", "en" };

            var contieneIndicador = palabrasPregunta.Any(p => indicadores.Contains(p))
                || palabrasPregunta.Any(p => indicadorPrefijos.Any(pref => p == pref || p.StartsWith(pref) && p.Length > pref.Length));

            var candidates = new List<(string nombre, int coincidencias, int totalPalabras, int chunkCount)>();

            _logger.LogInformation("Nombres de documentos disponibles: [{Names}]",
                string.Join("; ", documentCounts.Select(c => $"{c.Nombre} ({c.ChunkCount} chunks)")));

            var chunkCounts = documentCounts.ToDictionary(c => c.Nombre, c => c.ChunkCount, StringComparer.OrdinalIgnoreCase);

            foreach (var nombre in documentNames)
            {
                if (string.IsNullOrEmpty(nombre)) continue;

                var nombreLower = Normalizar(nombre);
                var separadoresDoc = new[] { ' ', '_', '-', '/', '\\' };
                var palabrasNombre = nombreLower.Split(separadoresDoc, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => p.Length >= 2)
                    .ToList();

                if (palabrasNombre.Count == 0) continue;

                var coincidencias = palabrasNombre.Count(p => preguntaLower.Contains(p));
                var cc = chunkCounts.TryGetValue(nombre, out var cnt) ? cnt : 0;

                candidates.Add((nombre, coincidencias, palabrasNombre.Count, cc));
            }

            if (candidates.Count == 0)
                return null;

            _logger.LogInformation("Candidatos de documento: [{Candidates}]",
                string.Join("; ", candidates.Select(c => $"'{c.nombre}' ({c.coincidencias}/{c.totalPalabras}, {c.chunkCount} chunks)")));

            var best = candidates
                .OrderByDescending(c => (double)c.coincidencias / c.totalPalabras)
                .ThenByDescending(c => c.coincidencias)
                .First();

            var ratio = (double)best.coincidencias / best.totalPalabras;
            
            // Umbrales diferentes según si hay indicador explícito
            var umbralRatio = contieneIndicador ? 0.5 : 1.0; // Sin indicador, requiere coincidencia completa
            var umbralCoincidencias = contieneIndicador ? 2 : 1;
            
            if (ratio < umbralRatio && best.coincidencias < umbralCoincidencias)
            {
                _logger.LogInformation("Documento detectado pero coincidencia débil: '{DocumentName}' ({Matches}/{Total}). Ignorando.",
                    best.nombre, best.coincidencias, best.totalPalabras);
                return null;
            }

            _logger.LogInformation("Documento detectado: '{DocumentName}' ({Matches}/{Total} palabras coincidentes en la pregunta). Indicador: {Indicador}",
                best.nombre, best.coincidencias, best.totalPalabras, contieneIndicador ? "si" : "no");
            return best.nombre;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detectando documento en la pregunta.");
        }

        return null;
    }
}
