using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class RagService : IRagService
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IEmbeddingConfiguracionRepository _configRepository;
    private readonly ILogger<RagService> _logger;

    public RagService(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        IEmbeddingConfiguracionRepository configRepository,
        ILogger<RagService> logger)
    {
        _vectorStore = vectorStore;
        _embeddingProvider = embeddingProvider;
        _configRepository = configRepository;
        _logger = logger;
    }

    private async Task<(List<FragmentoRelevanteDto> resultados, string? preferenciaDocUsada)> RecuperarResultadosCoreAsync(
        string consulta, int k, float puntajeMinimo, string? preferenciaDoc)
    {
        bool esPreguntaLiteral = SeccionDetectorService.EsPreguntaDeLista(consulta);
        string? seccionDetectada = SeccionDetectorService.DetectarSeccion(consulta);
        bool modoSeccion = SeccionDetectorService.EsConsultaDocumentalLiteral(consulta) ||
                           esPreguntaLiteral ||
                           seccionDetectada != null;

        var resultados = new List<FragmentoRelevanteDto>();

        try
        {
            string consultaBusqueda = consulta;
            if (modoSeccion && !string.IsNullOrWhiteSpace(seccionDetectada))
            {
                consultaBusqueda = seccionDetectada;
                _logger.LogInformation("Modo sección: usando '{Titulo}' como consulta de búsqueda en vez de '{Original}'",
                    seccionDetectada, consulta.Length > 60 ? consulta.Substring(0, 60) + "..." : consulta);
            }

            var resultadosBusqueda = await _vectorStore.SearchAsync(consultaBusqueda, k);

            if (resultadosBusqueda != null)
            {
                int rank = 0;
                resultados = resultadosBusqueda
                    .Where(r => r.Score >= puntajeMinimo)
                    .Select(r => new FragmentoRelevanteDto
                    {
                        DocumentId = r.DocumentId,
                        ChunkId = r.ChunkId,
                        DocumentoProcesadoId = r.DocumentoProcesadoId,
                        Texto = r.Text,
                        PuntajeSimilitud = r.Score,
                        Orden = r.Orden,
                        DocumentoNombre = r.MetadataDocumentoNombre,
                        DocumentoCodigo = r.MetadataDocumentoCodigo,
                        SearchRank = rank++
                    })
                    .ToList();

                foreach (var r in resultados)
                {
                    _logger.LogWarning("RAG RESULT: Doc='{Doc}', Score={Score:F4}, Text='{Text}'",
                        r.DocumentoNombre, r.PuntajeSimilitud,
                        r.Texto.Length > 120 ? r.Texto.Substring(0, 120) + "..." : r.Texto);
                }
            }

            if (!string.IsNullOrWhiteSpace(preferenciaDoc) &&
                !resultados.Any(r => NombreDocumentoCoincide(r.DocumentoNombre ?? "", preferenciaDoc)))
            {
                var consultaDirigida = $"{preferenciaDoc} {seccionDetectada ?? SeccionDetectorService.ExtraerTituloTema(consulta) ?? ""}".Trim();
                _logger.LogInformation(
                    "Documento preferido '{Pref}' no estaba en Top-K. Reintentando búsqueda: '{Consulta}'",
                    preferenciaDoc, consultaDirigida);

                var busquedaDirigida = await _vectorStore.SearchAsync(consultaDirigida, Math.Max(k, 15));
                if (busquedaDirigida != null)
                {
                    var extras = busquedaDirigida
                        .Where(r => NombreDocumentoCoincide(r.MetadataDocumentoNombre ?? "", preferenciaDoc))
                        .Select((r, idx) => new FragmentoRelevanteDto
                        {
                            DocumentId = r.DocumentId,
                            ChunkId = r.ChunkId,
                            DocumentoProcesadoId = r.DocumentoProcesadoId,
                            Texto = r.Text,
                            PuntajeSimilitud = Math.Max(r.Score, 0.5f),
                            Orden = r.Orden,
                            DocumentoNombre = r.MetadataDocumentoNombre,
                            DocumentoCodigo = r.MetadataDocumentoCodigo,
                            SearchRank = idx
                        })
                        .ToList();

                    if (extras.Count > 0)
                        resultados = extras;
                }
            }
            else if (!string.IsNullOrWhiteSpace(preferenciaDoc))
            {
                var filtrados = resultados
                    .Where(r => NombreDocumentoCoincide(r.DocumentoNombre ?? "", preferenciaDoc))
                    .ToList();
                if (filtrados.Count > 0)
                    resultados = filtrados;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al buscar en la base vectorial. Continuando sin contexto documental.");
        }

        return (resultados, preferenciaDoc);
    }

    public async Task<RagContextoDto> RecuperarContextoDocumentalAsync(string consulta, int? topK = null, string? savedDocumentPreference = null)
    {
        var cronometro = Stopwatch.StartNew();

        var config = await _configRepository.GetActivaAsync();
        var k = topK ?? config?.CantidadResultados ?? 8;
        var puntajeMinimo = config?.PuntajeMinimo ?? 0.15;
        var longitudMaxima = config?.LongitudMaximaContexto ?? 4000;

        string? preferenciaDoc = SeccionDetectorService.ExtraerPreferenciaDocumento(consulta);
        // If no current preference but we have a saved one, use the saved one!
        if (string.IsNullOrWhiteSpace(preferenciaDoc) && !string.IsNullOrWhiteSpace(savedDocumentPreference))
        {
            preferenciaDoc = savedDocumentPreference;
        }

        bool esPreguntaLiteral = SeccionDetectorService.EsPreguntaDeLista(consulta);
        string? seccionDetectada = SeccionDetectorService.DetectarSeccion(consulta);
        bool modoSeccion = SeccionDetectorService.EsConsultaDocumentalLiteral(consulta) ||
                           esPreguntaLiteral ||
                           seccionDetectada != null;

        // En modo sección ampliamos un poco el Top-K, pero NUNCA el documento entero.
        if (modoSeccion)
        {
            k = Math.Max(k, 8);
            longitudMaxima = Math.Min(Math.Max(longitudMaxima, 3500), 4500);
        }

        // First try with the document preference
        (List<FragmentoRelevanteDto> resultados, string? preferenciaDocUsada) = await RecuperarResultadosCoreAsync(consulta, k, (float)puntajeMinimo, preferenciaDoc);
        
        bool usarSavedPreference = !string.IsNullOrWhiteSpace(savedDocumentPreference) && 
                                   string.IsNullOrWhiteSpace(SeccionDetectorService.ExtraerPreferenciaDocumento(consulta));
        bool necesitaFallback = usarSavedPreference;
        List<FragmentoRelevanteDto>? resultadosExtraidos = null;
        bool fallbackTriggered = false;

        // If in modoSeccion, try to extract the section first; if results are empty OR don't contain the section title, fall back
        if (necesitaFallback && modoSeccion && resultados.Count > 0)
        {
            try
            {
                resultadosExtraidos = await ExtraerSoloSeccionRelevanteAsync(resultados, consulta, seccionDetectada, preferenciaDoc);
                bool isResultValid = resultadosExtraidos.Count > 0;
                if (isResultValid && !string.IsNullOrWhiteSpace(seccionDetectada))
                {
                    // Check if the extracted text actually contains the section title
                    string extractedText = resultadosExtraidos.First().Texto;
                    if (!SectionExtractorHelper.QuitarAcentos(extractedText).Contains(SectionExtractorHelper.QuitarAcentos(seccionDetectada)))
                    {
                        _logger.LogInformation("Extracted text doesn't contain the section title. Trying again without preference.");
                        isResultValid = false;
                    }
                }
                if (!isResultValid)
                {
                    _logger.LogInformation("Section not found in saved document preference (or invalid result). Trying again without preference.");
                    // Fall back: retrieve again
                    (resultados, preferenciaDocUsada) = await RecuperarResultadosCoreAsync(consulta, k, (float)puntajeMinimo, null);
                    preferenciaDoc = null; // Don't use preference for section extraction in fallback
                    resultadosExtraidos = null;
                    fallbackTriggered = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al extraer sección con preferencia, intentando sin preferencia.");
                (resultados, preferenciaDocUsada) = await RecuperarResultadosCoreAsync(consulta, k, (float)puntajeMinimo, null);
                preferenciaDoc = null;
                resultadosExtraidos = null;
                fallbackTriggered = true;
            }
        }
        else if (necesitaFallback && resultados.Count == 0)
        {
            _logger.LogInformation("No results found with saved document preference. Trying again without preference.");
            (resultados, preferenciaDocUsada) = await RecuperarResultadosCoreAsync(consulta, k, (float)puntajeMinimo, null);
            preferenciaDoc = null;
            fallbackTriggered = true;
        }

        _logger.LogInformation("Recuperando contexto documental para: '{Consulta}'. Top-K: {TopK}, Puntaje mínimo: {Puntaje}, Sección: {Seccion}",
            consulta.Length > 100 ? consulta[..100] + "..." : consulta, k, puntajeMinimo, seccionDetectada ?? "-");

        if (modoSeccion && resultados.Count > 0)
        {
            var resultadosOriginales = new List<FragmentoRelevanteDto>(resultados);
            if (fallbackTriggered || resultadosExtraidos == null || resultadosExtraidos.Count == 0)
            {
                // Re-extract with the new (fallback) results
                try
                {
                    resultados = await ExtraerSoloSeccionRelevanteAsync(resultados, consulta, seccionDetectada, preferenciaDoc);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al extraer sección relevante; se mantienen solo los chunks Top-K.");
                }
            }
            else
            {
                resultados = resultadosExtraidos;
            }

            // If section extraction returned empty but we had good search results, keep the original results
            if (resultados.Count == 0 && resultadosOriginales.Count > 0)
            {
                _logger.LogInformation("Extracción de sección vacía; usando {Count} fragmentos originales de la búsqueda.", resultadosOriginales.Count);
                resultados = resultadosOriginales;
            }
        }

        cronometro.Stop();

        var contextoDocumental = ConstruirContextoDocumental(resultados, longitudMaxima, consulta, seccionDetectada);

        var tokensEstimados = contextoDocumental.Length / 4;

        return new RagContextoDto
        {
            ContextoDocumental = contextoDocumental,
            FragmentosRecuperados = resultados,
            TotalFragmentos = resultados.Count,
            TokensEstimadosContexto = tokensEstimados,
            TiempoRecuperacionMs = cronometro.ElapsedMilliseconds
        };
    }

    private async Task<List<FragmentoRelevanteDto>> ExtraerSoloSeccionRelevanteAsync(
        List<FragmentoRelevanteDto> resultados,
        string consulta,
        string? seccionDetectada,
        string? preferenciaDoc = null)
    {
        var grupos = resultados
            .GroupBy(r => r.DocumentoProcesadoId)
            .Select(g => new
            {
                DocId = g.Key,
                Hits = g.ToList(),
                MinRank = g.Min(r => r.SearchRank),
                MaxScore = g.Max(r => r.PuntajeSimilitud),
                Nombre = g.First().DocumentoNombre ?? ""
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferenciaDoc))
        {
            var preferidos = grupos
                .Where(g => NombreDocumentoCoincide(g.Nombre, preferenciaDoc))
                .OrderBy(g => g.MinRank)
                .ThenByDescending(g => g.MaxScore)
                .ToList();
            if (preferidos.Count > 0)
                grupos = preferidos;
        }

            var tituloBusqueda = seccionDetectada ?? SeccionDetectorService.ExtraerTituloTema(consulta);
        if (string.IsNullOrWhiteSpace(tituloBusqueda))
        {
            _logger.LogWarning("Sin título de búsqueda. Devolviendo Top-2 chunks.");
            var fallbackDoc = grupos.OrderBy(g => g.MinRank).First();
            return fallbackDoc.Hits.OrderByDescending(f => f.PuntajeSimilitud).Take(2).ToList();
        }

        var tituloSinAcentos = SectionExtractorHelper.QuitarAcentos(tituloBusqueda.Trim());
        var tituloFlexible = SectionExtractorHelper.NormalizarTituloParaBusqueda(tituloBusqueda);
        var tituloFlexWords = tituloFlexible.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Match either numbered section (e.g., "8. Productividad y Seguimiento") OR unnumbered (e.g., "Productividad y Seguimiento")
        // Only match at the START OF A LINE to prevent false positives!
        // Match numbered sections in original format (e.g., "9. Glosario") OR markdown format (e.g., "**9.** Glosario")
        // También acepta artículos extra: "Bienestar y la Ergonomia" ≈ "Bienestar y Ergonomia"
        string patronTitulo;
        if (tituloFlexWords.Length >= 2)
        {
            patronTitulo = string.Join(@"\s+(?:\S+\s+){0,2}", tituloFlexWords.Select(Regex.Escape));
        }
        else
        {
            patronTitulo = Regex.Escape(tituloSinAcentos);
        }
        var regexTitulo = new System.Text.RegularExpressions.Regex(
            @"^(?:\*{0,2}(\d{1,2})\.\*{0,2}\s*)?" + patronTitulo,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

        // Buscar en TODOS los documentos cuál tiene el título de sección
        foreach (var candidato in grupos.OrderBy(g => g.MinRank).ThenByDescending(g => g.MaxScore))
        {
            var docId = candidato.DocId;
            var todosLosChunks = (await _vectorStore.GetByDocumentoProcesadoIdAsync(docId))?.ToList();
            if (todosLosChunks == null || todosLosChunks.Count == 0)
                continue;

            var chunksOrdenados = todosLosChunks.OrderBy(c => c.Orden).ToList();

            int idxChunkTitulo = -1;
            for (int ci = 0; ci < chunksOrdenados.Count; ci++)
            {
                var chunkText = SectionExtractorHelper.LimpiarTextoPdf(chunksOrdenados[ci].Text);
                var textNorm = SectionExtractorHelper.QuitarAcentos(chunkText);
                var match = regexTitulo.Match(textNorm);
                if (match.Success && (match.Index == 0 || textNorm[match.Index - 1] == '\n' || textNorm[match.Index - 1] == '\r'))
                {
                    idxChunkTitulo = ci;
                    break;
                }
            }

            // Fallback: if not found at start of line, search for the title anywhere in any chunk
            if (idxChunkTitulo < 0)
            {
                var tituloLower = tituloSinAcentos.ToLowerInvariant();
                var flexLower = tituloFlexible;
                for (int ci = 0; ci < chunksOrdenados.Count; ci++)
                {
                    var chunkText = SectionExtractorHelper.LimpiarTextoPdf(chunksOrdenados[ci].Text);
                    var textNorm = SectionExtractorHelper.QuitarAcentos(chunkText).ToLowerInvariant();
                    var textFlex = SectionExtractorHelper.NormalizarTituloParaBusqueda(chunkText);
                    bool matchExact = textNorm.Contains(tituloLower);
                    bool matchFlex = tituloFlexWords.Length >= 2 &&
                                     tituloFlexWords.All(w => textFlex.Contains(w, StringComparison.Ordinal));
                    if (matchExact || matchFlex)
                    {
                        idxChunkTitulo = ci;
                        break;
                    }
                }
            }

            if (idxChunkTitulo < 0)
                continue;

            var chunkTitulo = chunksOrdenados[idxChunkTitulo];
            
            // Verify the match in the FULL document text to avoid false positives!
            string? rawDocText = await _vectorStore.GetDocumentTextAsync(docId);
            if (string.IsNullOrWhiteSpace(rawDocText))
                continue;
            // Apply LimpiarTextoPdf to fix PDF extraction issues (missing spaces, glued text, etc.)
            string fullDocText = SectionExtractorHelper.LimpiarTextoPdf(rawDocText);
            string fullDocTextNorm = SectionExtractorHelper.QuitarAcentos(fullDocText);

            var secMatchesInFullDoc = regexTitulo.Matches(fullDocTextNorm);
            int desdeInFullDoc = -1;
            bool foundByRegex = false;
            _logger.LogWarning("SECTION_DEBUG: Title '{Title}', regex matches: {Count}, fullDoc len: {Len}",
                tituloBusqueda, secMatchesInFullDoc.Count, fullDocTextNorm.Length);
            foreach (System.Text.RegularExpressions.Match m in secMatchesInFullDoc)
            {
                _logger.LogWarning("SECTION_DEBUG: match at {Idx}, text: '{Text}'", m.Index,
                    fullDocTextNorm.Substring(m.Index, Math.Min(80, fullDocTextNorm.Length - m.Index)));
            }
            // Take the LAST match (skip TOC entries, prefer actual content)
            foreach (System.Text.RegularExpressions.Match match in secMatchesInFullDoc)
            {
                if (match.Success && (match.Index == 0 || fullDocTextNorm[match.Index - 1] == '\n' || fullDocTextNorm[match.Index - 1] == '\r'))
                {
                    desdeInFullDoc = match.Index;
                    foundByRegex = true;
                }
            }
            // Fallback: search for the title anywhere in the full document text
            if (desdeInFullDoc < 0)
            {
                desdeInFullDoc = fullDocTextNorm.IndexOf(tituloSinAcentos, StringComparison.OrdinalIgnoreCase);
            }
            // Fallback flexible: palabras clave del título (sin artículos) en ventana corta
            if (desdeInFullDoc < 0 && tituloFlexWords.Length >= 2)
            {
                var lowerDoc = fullDocTextNorm.ToLowerInvariant();
                int searchFrom = 0;
                while (searchFrom < lowerDoc.Length)
                {
                    int first = lowerDoc.IndexOf(tituloFlexWords[0], searchFrom, StringComparison.Ordinal);
                    if (first < 0) break;
                    int cursor = first + tituloFlexWords[0].Length;
                    bool allNear = true;
                    for (int wi = 1; wi < tituloFlexWords.Length; wi++)
                    {
                        int p = lowerDoc.IndexOf(tituloFlexWords[wi], cursor, StringComparison.Ordinal);
                        if (p < 0 || p - first > Math.Max(tituloBusqueda.Length + 20, 80))
                        {
                            allNear = false;
                            break;
                        }
                        cursor = p + tituloFlexWords[wi].Length;
                    }
                    if (allNear)
                    {
                        // Preferir la última coincidencia (contenido real vs TOC)
                        desdeInFullDoc = first;
                        searchFrom = first + 1;
                        continue;
                    }
                    searchFrom = first + 1;
                }
            }
            if (desdeInFullDoc < 0)
                continue;
            
            // If found by regex, validate with regex; if found by IndexOf fallback, skip regex check
            if (foundByRegex)
            {
                var secMatch = regexTitulo.Match(fullDocTextNorm, desdeInFullDoc);
                if (!secMatch.Success)
                    continue;
            }

            // Look back to find the section number if present
            bool esNumerada = false;
            int numSec = 0;
            int lookbackStart = Math.Max(0, desdeInFullDoc - 20);
            string lookbackText = fullDocTextNorm.Substring(lookbackStart, desdeInFullDoc - lookbackStart);
            var numLookback = System.Text.RegularExpressions.Regex.Match(lookbackText, @"(\d{1,2})\.\s*$");
            if (numLookback.Success)
            {
                esNumerada = true;
                int.TryParse(numLookback.Groups[1].Value, out numSec);
            }

            var plantilla = candidato.Hits.OrderByDescending(f => f.PuntajeSimilitud).First();

            // Now use the full document text to extract the section properly!
            int desde = desdeInFullDoc;
            int hasta = fullDocText.Length;

            // Find next numbered section (supports original "9. Title" and markdown "**9.**" formats).
            // Reject subsection numbers like "4. Criterios" from "2.4 Criterios" using:
            //  - (?<!\d\.) — previene "4." cuando va precedido de "2." que es \d\.
            //  - Si la sección actual es numerada (numSec > 0), solo números > numSec
            int hastaNumerada = fullDocText.Length;
            var proxRegexNum = new System.Text.RegularExpressions.Regex(
                @"(?<!\d)(?<!\d\.)\*{0,2}(\d+)\.\*{0,2}\s*[A-ZÁÉÍÓÚÑ]",
                System.Text.RegularExpressions.RegexOptions.None);
            var proxMatchNum = proxRegexNum.Match(fullDocTextNorm, desde + 3);
            while (proxMatchNum.Success && proxMatchNum.Index > desde + 3)
            {
                int proxNum = int.Parse(proxMatchNum.Groups[1].Value);
                bool enNewLine = proxMatchNum.Index <= 1 ||
                    fullDocTextNorm[proxMatchNum.Index - 1] == '\n' ||
                    fullDocTextNorm[proxMatchNum.Index - 1] == '*' ||
                    (proxMatchNum.Index >= 2 && fullDocTextNorm[proxMatchNum.Index - 1] == ' ' && fullDocTextNorm[proxMatchNum.Index - 2] == '\n');
                bool esMayorQueActual = numSec > 0 ? proxNum > numSec : true;
                if (enNewLine && esMayorQueActual)
                {
                    hastaNumerada = proxMatchNum.Index;
                    break;
                }
                proxMatchNum = proxMatchNum.NextMatch();
            }

            // Find next unnumbered section: siempre buscar como fallback.
            // Requerir distancia mínima de 200 chars para evitar falsos positivos.
            int hastaUnnumerada = fullDocText.Length;
            var unnSecMatch = System.Text.RegularExpressions.Regex.Match(
                fullDocTextNorm.Substring(desde + 3),
                @"\n([A-ZÁÉÍÓÚÑ][a-záéíóúñ]{3,}\s+(?:de|del|y)\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)",
                System.Text.RegularExpressions.RegexOptions.None);
            if (unnSecMatch.Success && unnSecMatch.Index >= 200)
            {
                hastaUnnumerada = desde + 3 + unnSecMatch.Index;
            }

            hasta = Math.Min(hastaNumerada, hastaUnnumerada);

            _logger.LogWarning("SECTION_DEBUG: '{Title}' regex={Regex}, desde={Desde}, hastaNum={HN}, hastaUnnum={HU}, hasta={Hasta}, docLen={Len}",
                tituloBusqueda, patronTitulo, desde, hastaNumerada, hastaUnnumerada, hasta, fullDocText.Length);

            // Debug: mostrar contexto alrededor del título encontrado
            if (desde > 0 && desde < fullDocText.Length)
            {
                int ctxStart = Math.Max(0, desde - 30);
                int ctxLen = Math.Min(120, fullDocText.Length - ctxStart);
                string ctx = fullDocTextNorm.Substring(ctxStart, ctxLen).Replace("\n", "\\n");
                _logger.LogWarning("SECTION_DEBUG_CTX: ...{Context}...", ctx);
            }

            if (hasta > desde + 50 && hasta - desde <= 12000)
            {
                var textoSeccion = fullDocText.Substring(desde, hasta - desde).Trim();

                // Post-process: trim trailing section headers like "10." or "**10.**" (with optional preceding period)
                textoSeccion = System.Text.RegularExpressions.Regex.Replace(
                    textoSeccion, @"\.?\s*\*{0,2}\d{1,2}\.\*{0,2}\s*$", "").Trim();

                // Post-process: remove duplicate section title lines
                var lineas = textoSeccion.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (lineas.Length >= 2)
                {
                    // Check if last line is a duplicate of the section title or an earlier line
                    var ultima = lineas[^1];
                    var penultima = lineas.Length >= 2 ? lineas[^2] : "";
                    bool isDuplicate = false;
                    // Duplicate of section title at start
                    if (lineas.Length >= 3 && lineas[^1] == lineas[0])
                        isDuplicate = true;
                    // Duplicate content (same line repeated)
                    if (ultima.Length > 20 && penultima.Contains(ultima[..Math.Min(20, ultima.Length)]))
                        isDuplicate = true;
                    // Last line matches section header pattern (e.g. "Glosario de Terminos" appearing as plain text)
                    var firstLineNorm = SectionExtractorHelper.QuitarAcentos(lineas[0]).ToLowerInvariant();
                    var lastLineNorm = SectionExtractorHelper.QuitarAcentos(ultima).ToLowerInvariant();
                    if (lineas.Length >= 3 && lastLineNorm.Length > 5 &&
                        firstLineNorm.Replace(" ", "").Contains(lastLineNorm.Replace(" ", "")))
                        isDuplicate = true;

                    if (isDuplicate)
                        textoSeccion = string.Join("\n", lineas[..^1]).Trim();
                }

                _logger.LogWarning("Sección extraída ({Len} chars) de documento {Doc}",
                    textoSeccion.Length, plantilla.DocumentoNombre);
                return new List<FragmentoRelevanteDto>
                {
                    new()
                    {
                        DocumentId = plantilla.DocumentId,
                        ChunkId = chunkTitulo.ChunkId,
                        DocumentoProcesadoId = docId,
                        Texto = textoSeccion,
                        PuntajeSimilitud = plantilla.PuntajeSimilitud,
                        Orden = chunkTitulo.Orden,
                        DocumentoNombre = plantilla.DocumentoNombre,
                        DocumentoCodigo = plantilla.DocumentoCodigo,
                        SearchRank = 0
                    }
                };
            }
        }

        _logger.LogWarning("Sección '{Title}' no encontrada en documentos del ranking. Escaneando TODOS los documentos indexados...", tituloBusqueda);

        var todosLosDocIds = (await _vectorStore.GetAllDocumentoProcesadoIdsAsync()).ToList();
        var docIdsEnGrupos = grupos.Select(g => g.DocId).ToHashSet();
        var docIdsNuevos = todosLosDocIds.Where(id => !docIdsEnGrupos.Contains(id)).ToList();

        foreach (var docId in docIdsNuevos)
        {
            var todosLosChunks = (await _vectorStore.GetByDocumentoProcesadoIdAsync(docId))?.ToList();
            if (todosLosChunks == null || todosLosChunks.Count == 0)
                continue;

            // If we have a document preference, only process docs that match the preference!
            string? nombreDoc = todosLosChunks.FirstOrDefault()?.MetadataDocumentoNombre;
            if (!string.IsNullOrWhiteSpace(preferenciaDoc) && !string.IsNullOrWhiteSpace(nombreDoc) && !NombreDocumentoCoincide(nombreDoc, preferenciaDoc))
            {
                continue; // Skip non-matching docs
            }

            var chunksOrdenados = todosLosChunks.OrderBy(c => c.Orden).ToList();

            int idxChunkTitulo = -1;
            for (int ci = 0; ci < chunksOrdenados.Count; ci++)
            {
                var chunkText = SectionExtractorHelper.LimpiarTextoPdf(chunksOrdenados[ci].Text);
                var textNorm = SectionExtractorHelper.QuitarAcentos(chunkText);
                var match = regexTitulo.Match(textNorm);
                if (match.Success && (match.Index == 0 || textNorm[match.Index - 1] == '\n' || textNorm[match.Index - 1] == '\r'))
                {
                    idxChunkTitulo = ci;
                    break;
                }
            }

            // Fallback: if not found at start of line, search for the title anywhere in any chunk
            if (idxChunkTitulo < 0)
            {
                var tituloLower = tituloSinAcentos.ToLowerInvariant();
                for (int ci = 0; ci < chunksOrdenados.Count; ci++)
                {
                    var chunkText = SectionExtractorHelper.LimpiarTextoPdf(chunksOrdenados[ci].Text);
                    var textNorm = SectionExtractorHelper.QuitarAcentos(chunkText).ToLowerInvariant();
                    var textFlex = SectionExtractorHelper.NormalizarTituloParaBusqueda(chunkText);
                    bool matchExact = textNorm.Contains(tituloLower);
                    bool matchFlex = tituloFlexWords.Length >= 2 &&
                                     tituloFlexWords.All(w => textFlex.Contains(w, StringComparison.Ordinal));
                    if (matchExact || matchFlex)
                    {
                        idxChunkTitulo = ci;
                        break;
                    }
                }
            }

            if (idxChunkTitulo < 0)
                continue;

            var chunkTitulo = chunksOrdenados[idxChunkTitulo];
            
            // Verify the match in the FULL document text to avoid false positives!
            string? rawDocText2 = await _vectorStore.GetDocumentTextAsync(docId);
            if (string.IsNullOrWhiteSpace(rawDocText2))
                continue;
            // Apply LimpiarTextoPdf to fix PDF extraction issues (missing spaces, glued text, etc.)
            string fullDocText = SectionExtractorHelper.LimpiarTextoPdf(rawDocText2);
            string fullDocTextNorm = SectionExtractorHelper.QuitarAcentos(fullDocText);
            var secMatchesInFullDoc = regexTitulo.Matches(fullDocTextNorm);
            int desdeInFullDoc = -1;
            bool foundByRegex2 = false;
            // Take the LAST match (skip TOC entries, prefer actual content)
            foreach (System.Text.RegularExpressions.Match match in secMatchesInFullDoc)
            {
                if (match.Success && (match.Index == 0 || fullDocTextNorm[match.Index - 1] == '\n' || fullDocTextNorm[match.Index - 1] == '\r'))
                {
                    desdeInFullDoc = match.Index;
                    foundByRegex2 = true;
                }
            }
            // Fallback: search for the title anywhere in the full document text
            if (desdeInFullDoc < 0)
            {
                desdeInFullDoc = fullDocTextNorm.IndexOf(tituloSinAcentos, StringComparison.OrdinalIgnoreCase);
            }
            if (desdeInFullDoc < 0 && tituloFlexWords.Length >= 2)
            {
                var lowerDoc = fullDocTextNorm.ToLowerInvariant();
                int searchFrom = 0;
                while (searchFrom < lowerDoc.Length)
                {
                    int first = lowerDoc.IndexOf(tituloFlexWords[0], searchFrom, StringComparison.Ordinal);
                    if (first < 0) break;
                    int cursor = first + tituloFlexWords[0].Length;
                    bool allNear = true;
                    for (int wi = 1; wi < tituloFlexWords.Length; wi++)
                    {
                        int p = lowerDoc.IndexOf(tituloFlexWords[wi], cursor, StringComparison.Ordinal);
                        if (p < 0 || p - first > Math.Max(tituloBusqueda.Length + 20, 80))
                        {
                            allNear = false;
                            break;
                        }
                        cursor = p + tituloFlexWords[wi].Length;
                    }
                    if (allNear)
                    {
                        desdeInFullDoc = first;
                        searchFrom = first + 1;
                        continue;
                    }
                    searchFrom = first + 1;
                }
            }
            if (desdeInFullDoc < 0)
                continue;
            
            if (foundByRegex2)
            {
                var secMatch = regexTitulo.Match(fullDocTextNorm, desdeInFullDoc);
                if (!secMatch.Success)
                    continue;
            }

            // Determine section number by looking back from the title position
            bool esNumerada = false;
            int numSec = 0;
            int lookbackStart2 = Math.Max(0, desdeInFullDoc - 20);
            string lookbackText2 = fullDocTextNorm.Substring(lookbackStart2, desdeInFullDoc - lookbackStart2);
            var numLookback2 = System.Text.RegularExpressions.Regex.Match(lookbackText2, @"(\d{1,2})\.\s*$");
            if (numLookback2.Success)
            {
                esNumerada = true;
                int.TryParse(numLookback2.Groups[1].Value, out numSec);
            }

            // Now use the full document text to extract the section properly!
            int desde = desdeInFullDoc;
            int hasta = fullDocText.Length;

            // Find next numbered section (supports both "9. Title" and "**9.**" formats).
            int hastaNumerada = fullDocText.Length;
            var proxRegexNum = new System.Text.RegularExpressions.Regex(
                @"(?<!\d)(?<!\d\.)\*{0,2}(\d+)\.\*{0,2}\s*[A-ZÁÉÍÓÚÑ]",
                System.Text.RegularExpressions.RegexOptions.None);
            var proxMatchNum = proxRegexNum.Match(fullDocTextNorm, desde + 3);
            while (proxMatchNum.Success && proxMatchNum.Index > desde + 3)
            {
                int proxNum = int.Parse(proxMatchNum.Groups[1].Value);
                bool enNewLine = proxMatchNum.Index <= 1 ||
                    fullDocTextNorm[proxMatchNum.Index - 1] == '\n' ||
                    fullDocTextNorm[proxMatchNum.Index - 1] == '*' ||
                    (proxMatchNum.Index >= 2 && fullDocTextNorm[proxMatchNum.Index - 1] == ' ' && fullDocTextNorm[proxMatchNum.Index - 2] == '\n');
                bool esMayorQueActual = numSec > 0 ? proxNum > numSec : true;
                if (enNewLine && esMayorQueActual)
                {
                    hastaNumerada = proxMatchNum.Index;
                    break;
                }
                proxMatchNum = proxMatchNum.NextMatch();
            }

            // Find next unnumbered section: siempre buscar como fallback.
            int hastaUnnumerada = fullDocText.Length;
            var unnSecMatch = System.Text.RegularExpressions.Regex.Match(
                fullDocTextNorm.Substring(desde + 3),
                @"\n([A-ZÁÉÍÓÚÑ][a-záéíóúñ]{3,}\s+(?:de|del|y)\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+)",
                System.Text.RegularExpressions.RegexOptions.None);
            if (unnSecMatch.Success && unnSecMatch.Index >= 200)
            {
                hastaUnnumerada = desde + 3 + unnSecMatch.Index;
            }

            hasta = Math.Min(hastaNumerada, hastaUnnumerada);

            if (hasta > desde + 50 && hasta - desde <= 12000)
            {
                var textoSeccion = fullDocText.Substring(desde, hasta - desde).Trim();
                // Post-process: trim trailing section headers like "10." or "**10.**"
                textoSeccion = System.Text.RegularExpressions.Regex.Replace(
                    textoSeccion, @"\.?\s*\*{0,2}\d{1,2}\.\*{0,2}\s*$", "").Trim();
                // Post-process: remove duplicate section title lines
                var lineas = textoSeccion.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (lineas.Length >= 2)
                {
                    var ultima = lineas[^1];
                    var penultima = lineas[^2];
                    bool isDuplicate = false;
                    if (lineas.Length >= 3 && lineas[^1] == lineas[0])
                        isDuplicate = true;
                    if (ultima.Length > 20 && penultima.Contains(ultima[..Math.Min(20, ultima.Length)]))
                        isDuplicate = true;
                    var firstLineNorm = SectionExtractorHelper.QuitarAcentos(lineas[0]).ToLowerInvariant();
                    var lastLineNorm = SectionExtractorHelper.QuitarAcentos(ultima).ToLowerInvariant();
                    if (lineas.Length >= 3 && lastLineNorm.Length > 5 &&
                        firstLineNorm.Replace(" ", "").Contains(lastLineNorm.Replace(" ", "")))
                        isDuplicate = true;
                    if (isDuplicate)
                        textoSeccion = string.Join("\n", lineas[..^1]).Trim();
                }
                _logger.LogInformation("Sección '{Title}' encontrada por escaneo completo ({Len} chars) en documento {Doc}",
                    tituloBusqueda, textoSeccion.Length, nombreDoc);
                return new List<FragmentoRelevanteDto>
                {
                    new()
                    {
                        DocumentId = chunkTitulo.DocumentId,
                        ChunkId = chunkTitulo.ChunkId,
                        DocumentoProcesadoId = docId,
                        Texto = textoSeccion,
                        PuntajeSimilitud = 1.0f,
                        Orden = chunkTitulo.Orden,
                        DocumentoNombre = nombreDoc,
                        DocumentoCodigo = chunksOrdenados.First().MetadataDocumentoCodigo,
                        SearchRank = 0
                    }
                };
            }
        }

        _logger.LogWarning("Ningún documento contiene la sección '{Title}'. Devolviendo lista vacía.", tituloBusqueda);
        return new List<FragmentoRelevanteDto>();
    }

    private static string? ExtraerSeccionDirecta(string texto, string? titulo)
    {
        if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(titulo))
            return null;

        var textoSinAcentos = SectionExtractorHelper.QuitarAcentos(texto);
        var tituloSinAcentos = SectionExtractorHelper.QuitarAcentos(titulo.Trim());

        var seccionRegex = new System.Text.RegularExpressions.Regex(
            @"^(?:\*{0,2}(\d{1,2})\.\*{0,2}\s*)?" + System.Text.RegularExpressions.Regex.Escape(tituloSinAcentos),
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
        var seccionMatch = seccionRegex.Match(textoSinAcentos);

        int numSec = 0;
        int inicioSeccion;
        if (seccionMatch.Success)
        {
            inicioSeccion = seccionMatch.Index;
            if (seccionMatch.Groups[1].Success)
                int.TryParse(seccionMatch.Groups[1].Value, out numSec);
        }
        else
        {
            int idxAnywhere = textoSinAcentos.IndexOf(tituloSinAcentos, StringComparison.OrdinalIgnoreCase);
            if (idxAnywhere < 0)
                return null;
            inicioSeccion = idxAnywhere;
        }

        int finSection = texto.Length;
        var proxRegexNum = new System.Text.RegularExpressions.Regex(
            @"(?<!\d)(?<!\d\.)\*{0,2}(\d+)\.\*{0,2}\s*[A-ZÁÉÍÓÚÑ]",
            System.Text.RegularExpressions.RegexOptions.None);
        var proxMatchNum = proxRegexNum.Match(textoSinAcentos, inicioSeccion + tituloSinAcentos.Length);
        while (proxMatchNum.Success && proxMatchNum.Index > inicioSeccion + tituloSinAcentos.Length)
        {
            int proxNum = int.Parse(proxMatchNum.Groups[1].Value);
            bool esMayor = numSec > 0 ? proxNum > numSec : true;
            bool enNewLine = proxMatchNum.Index <= 1 ||
                textoSinAcentos[proxMatchNum.Index - 1] == '\n' ||
                textoSinAcentos[proxMatchNum.Index - 1] == '*' ||
                (proxMatchNum.Index >= 2 && textoSinAcentos[proxMatchNum.Index - 1] == ' ' && textoSinAcentos[proxMatchNum.Index - 2] == '\n');
            if (enNewLine && esMayor)
            {
                finSection = proxMatchNum.Index;
                break;
            }
            proxMatchNum = proxMatchNum.NextMatch();
        }
        
        // Also check for next unnumbered section (siempre como fallback, con distancia mínima)
        var proxSeccionRegex = new System.Text.RegularExpressions.Regex(
            @"(?<=\n)[A-ZÁÉÍÓÚÑ][a-záéíóúñ]{3,}\s+(?:de|del|y)\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+",
            System.Text.RegularExpressions.RegexOptions.None);
        var proxMatchSeccion = proxSeccionRegex.Match(textoSinAcentos, inicioSeccion + tituloSinAcentos.Length);
        if (proxMatchSeccion.Success && proxMatchSeccion.Index > inicioSeccion + tituloSinAcentos.Length + 200 && proxMatchSeccion.Index < finSection)
            finSection = proxMatchSeccion.Index;

        if (finSection <= inicioSeccion)
            return null;

        return texto.Substring(inicioSeccion, finSection - inicioSeccion).Trim();
    }



    private static List<FragmentoRelevanteDto> LimitarAVentanaLocal(List<FragmentoRelevanteDto> resultados, int radio)
    {
        var porDoc = resultados
            .GroupBy(r => r.DocumentoProcesadoId)
            .OrderBy(g => g.Min(r => r.SearchRank))
            .Take(1)
            .SelectMany(g => g.OrderBy(f => f.Orden).Take(3 + radio))
            .ToList();

        return porDoc.Count > 0 ? porDoc : resultados.OrderBy(r => r.SearchRank).Take(5).ToList();
    }

    private static bool NombreDocumentoCoincide(string nombreDoc, string preferencia)
    {
        if (string.IsNullOrWhiteSpace(nombreDoc) || string.IsNullOrWhiteSpace(preferencia))
            return false;

        var n = SectionExtractorHelper.QuitarAcentos(nombreDoc).ToLowerInvariant();
        return preferencia switch
        {
            "onboarding" => n.Contains("onboarding") || n.Contains("incorporacion"),
            "cloudsync" => n.Contains("cloudsync") || (n.Contains("manual") && (n.Contains("producto") || n.Contains("cloud"))),
            "manual" => n.Contains("manual"),
            "guia" => n.Contains("guia") && (n.Contains("desarrollo") || n.Contains("software")),
            "politica" => n.Contains("politica") || n.Contains("teletrabajo"),
            _ => n.Contains(preferencia)
        };
    }

    public async Task<BusquedaSemanticaResponse> BuscarSemanticamenteAsync(string consulta, int? topK = null)
    {
        var cronometro = Stopwatch.StartNew();

        try
        {
            var config = await _configRepository.GetActivaAsync();
            var k = topK ?? config?.CantidadResultados ?? 8;

            _logger.LogInformation("Buscando semánticamente: '{Consulta}', TopK: {TopK}", consulta, k);

            var resultadosBusqueda = await _vectorStore.SearchAsync(consulta, k);

            cronometro.Stop();

            var fragmentos = (resultadosBusqueda ?? Enumerable.Empty<Domain.Entities.VectorSearchResult>())
                .Select(r => new FragmentoRelevanteDto
                {
                    DocumentId = r.DocumentId,
                    ChunkId = r.ChunkId,
                    DocumentoProcesadoId = r.DocumentoProcesadoId,
                    Texto = r.Text,
                    PuntajeSimilitud = r.Score,
                    Orden = r.Orden,
                    DocumentoNombre = r.MetadataDocumentoNombre,
                    DocumentoCodigo = r.MetadataDocumentoCodigo
                })
                .ToList();

            _logger.LogInformation("Búsqueda completada: {Count} fragmentos encontrados en {Time}ms", fragmentos.Count, cronometro.ElapsedMilliseconds);

            return new BusquedaSemanticaResponse
            {
                Consulta = consulta,
                TotalResultados = fragmentos.Count,
                Fragmentos = fragmentos,
                TiempoMs = cronometro.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            cronometro.Stop();
            _logger.LogError(ex, "Error en búsqueda semántica para: '{Consulta}'", consulta);
            
            return new BusquedaSemanticaResponse
            {
                Consulta = consulta,
                TotalResultados = 0,
                Fragmentos = new List<FragmentoRelevanteDto>(),
                TiempoMs = cronometro.ElapsedMilliseconds
            };
        }
    }

    private static string ConstruirContextoDocumental(
        List<FragmentoRelevanteDto> fragmentos,
        int longitudMaxima,
        string? consulta = null,
        string? seccionDetectada = null)
    {
        if (!fragmentos.Any())
            return string.Empty;

        bool modoSeccion = SeccionDetectorService.EsConsultaDocumentalLiteral(consulta ?? "") ||
                           SeccionDetectorService.EsPreguntaDeLista(consulta ?? "") ||
                           !string.IsNullOrWhiteSpace(seccionDetectada) ||
                           SeccionDetectorService.DetectarSeccion(consulta ?? "") != null;

        if (modoSeccion)
        {
            seccionDetectada ??= SeccionDetectorService.DetectarSeccion(consulta ?? "");

            var textosPorDoc = new List<string>();

            foreach (var grupo in fragmentos
                .GroupBy(f => f.DocumentoProcesadoId)
                .OrderBy(g => g.Min(f => f.SearchRank))
                .Take(1))
            {
                var ordenados = grupo.OrderBy(f => f.Orden)
                    .GroupBy(f => f.Texto.Trim())
                    .Select(g => g.First())
                    .ToList();

                var textoAIncluir = UnirChunksSinDuplicar(ordenados);

                // Limpiar texto PDF sucio ANTES de procesar
                textoAIncluir = SectionExtractorHelper.LimpiarTextoPdf(textoAIncluir);

                // Si hay múltiples fragmentos o el texto es muy largo, intentar recortar a la sección.
                if (ordenados.Count > 1 || textoAIncluir.Length > 600)
                {
                    var extraida = ExtraerSeccionDirecta(textoAIncluir, seccionDetectada)
                        ?? SectionExtractorHelper.ExtraerSeccionPorKeywords(
                            textoAIncluir, consulta ?? "", seccionDetectada);
                    if (extraida != null &&
                        extraida.Length >= 50 &&
                        (double)extraida.Length / textoAIncluir.Length < 0.55)
                    {
                        textoAIncluir = extraida;
                    }
                }

                if (textoAIncluir.Length > longitudMaxima)
                    textoAIncluir = textoAIncluir[..longitudMaxima] + "\n\n[... truncado ...]";

                var nombreDoc = ordenados.First().DocumentoNombre ?? "Documento";
                var codigoDoc = ordenados.First().DocumentoCodigo;
                
                // Formato limpio: encabezado + contenido con viñetas preservadas
                textosPorDoc.Add($"Según **{nombreDoc}**" + (string.IsNullOrEmpty(codigoDoc) ? "" : $" ({codigoDoc})") + ":\n\n" +
                                 $"**{seccionDetectada ?? "Contenido solicitado"}**\n\n" +
                                 FormatearComoLista(textoAIncluir));
            }

            var textoContexto = string.Join("\n\n---\n\n", textosPorDoc);

            return "=== CONTEXTO DOCUMENTAL (SECCIÓN) ===\n" +
                   $"Sección detectada: {seccionDetectada ?? "Contenido solicitado"}\n" +
                   "INSTRUCCIÓN: Responde ÚNICAMENTE con el contenido de la sección pedida.\n" +
                   "- Copia el texto de forma casi idéntica. NO parafrasees.\n" +
                   "- NO incluyas otras secciones del documento aunque aparezcan abajo.\n" +
                   "- NO añadas explicaciones propias.\n" +
                   "- Cita el nombre del documento al inicio.\n" +
                   "- Mantén el formato de lista si existe.\n\n" +
                   textoContexto +
                   "\n\n=== FIN CONTEXTO DOCUMENTAL ===";
        }

        var fragmentosAgrupados = fragmentos
            .GroupBy(f => f.DocumentoProcesadoId)
            .OrderByDescending(g => g.Max(f => f.PuntajeSimilitud))
            .ToList();

        var textoFinal = "";
        foreach (var grupo in fragmentosAgrupados)
        {
            if (textoFinal.Length >= longitudMaxima) break;

            var ordenados = grupo.OrderBy(f => f.Orden)
                .GroupBy(f => f.Texto.Trim())
                .Select(g => g.First())
                .ToList();
            var nombreDoc = ordenados.First().DocumentoNombre ?? "Documento";
            var textoUnido = UnirChunksSinDuplicar(ordenados);
            
            // Limpiar texto
            textoUnido = SectionExtractorHelper.LimpiarTextoPdf(textoUnido);

            var seccion = $"[Fuente: {nombreDoc}]\n{textoUnido}";

            if (textoFinal.Length + seccion.Length > longitudMaxima)
            {
                var espacio = longitudMaxima - textoFinal.Length;
                if (espacio > 200)
                    textoFinal += seccion[..espacio];
                break;
            }

            textoFinal += (textoFinal.Length > 0 ? "\n\n---\n\n" : "") + seccion;
        }

        if (string.IsNullOrWhiteSpace(textoFinal))
            return string.Empty;

        return "=== CONTEXTO DOCUMENTAL ===\n" +
               textoFinal +
               "\n\n=== FIN CONTEXTO DOCUMENTAL ===";
    }

    /// <summary>
    /// Formatea el texto preservando viñetas y estructura de lista.
    /// </summary>
    private static string FormatearComoLista(string texto)
        => SectionExtractorHelper.FormatearListaLimpia(texto);

    private static string UnirChunksSinDuplicar(List<FragmentoRelevanteDto> fragmentos)
    {
        if (fragmentos.Count == 0) return string.Empty;
        if (fragmentos.Count == 1) return fragmentos[0].Texto.Trim();

        var sb = new System.Text.StringBuilder();
        sb.Append(fragmentos[0].Texto.Trim());

        for (int i = 1; i < fragmentos.Count; i++)
        {
            var textoActual = fragmentos[i].Texto.Trim();
            var textoPrevio = sb.ToString();

            int overlapEncontrado = BuscarOverlapPorOraciones(textoPrevio, textoActual);

            if (overlapEncontrado > 0)
            {
                sb.Append("\n");
                sb.Append(textoActual.Substring(overlapEncontrado).TrimStart());
            }
            else
            {
                overlapEncontrado = BuscarOverlapPorCaracteres(textoPrevio, textoActual);
                if (overlapEncontrado > 0)
                {
                    sb.Append("\n");
                    sb.Append(textoActual.Substring(overlapEncontrado).TrimStart());
                }
                else
                {
                    sb.Append("\n\n");
                    sb.Append(textoActual);
                }
            }
        }

        return sb.ToString();
    }

    private static int BuscarOverlapPorOraciones(string textoPrevio, string textoActual)
    {
        var separadores = new[] { ". ", ".\n", "! ", "? ", ":\n" };
        var oracionesPrevias = textoPrevio.Split(separadores, StringSplitOptions.RemoveEmptyEntries);
        var oracionesActuales = textoActual.Split(separadores, StringSplitOptions.RemoveEmptyEntries);

        if (oracionesPrevias.Length == 0 || oracionesActuales.Length == 0)
            return 0;

        int maxProbe = Math.Min(oracionesPrevias.Length, 8);
        int maxProbeActual = Math.Min(oracionesActuales.Length, 8);

        for (int p = maxProbe; p >= 1; p--)
        {
            var ultimasPrevias = oracionesPrevias.Skip(oracionesPrevias.Length - p).ToArray();
            for (int a = maxProbeActual; a >= p; a--)
            {
                var primerasActuales = oracionesActuales.Take(a).ToArray();
                if (primerasActuales.Length >= ultimasPrevias.Length)
                {
                    bool match = true;
                    for (int j = 0; j < ultimasPrevias.Length; j++)
                    {
                        var previa = ultimasPrevias[j].Trim().ToLowerInvariant();
                        var actual = primerasActuales[primerasActuales.Length - ultimasPrevias.Length + j].Trim().ToLowerInvariant();
                        if (previa != actual && !previa.Contains(actual) && !actual.Contains(previa))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        string patronBusqueda = "";
                        for (int k = primerasActuales.Length - ultimasPrevias.Length; k < primerasActuales.Length; k++)
                        {
                            patronBusqueda += (k > primerasActuales.Length - ultimasPrevias.Length ? " " : "") + primerasActuales[k];
                        }

                        int idx = textoActual.IndexOf(patronBusqueda, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var textoDespues = textoActual.Substring(idx + patronBusqueda.Length);
                            idx = textoActual.Length - textoDespues.Length;
                            return idx;
                        }
                    }
                }
            }
        }

        return 0;
    }

    private static int BuscarOverlapPorCaracteres(string textoPrevio, string textoActual)
    {
        int maxProbe = Math.Min(400, Math.Min(textoPrevio.Length, textoActual.Length));

        for (int probe = maxProbe; probe >= 30; probe -= 5)
        {
            var finPrevio = textoPrevio.Substring(textoPrevio.Length - probe);
            int idx = textoActual.IndexOf(finPrevio, StringComparison.Ordinal);
            if (idx >= 0 && idx < textoActual.Length / 2)
            {
                return idx + probe;
            }

            var inicioActual = textoActual.Substring(0, probe);
            if (textoPrevio.EndsWith(inicioActual, StringComparison.Ordinal))
            {
                return probe;
            }
        }

        return 0;
    }
}
