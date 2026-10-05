using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

/// <summary>
/// RAG por comprensión: la consulta va íntegra a la búsqueda vectorial (los
/// embeddings entienden el significado), la preferencia de documento sale del
/// catálogo vivo (BD) y el contexto son los chunks top por score. Sin
/// diccionarios de secciones, sin listas de keywords, sin recorte por títulos:
/// el lector (modelo o entrega determinista) lee y entiende los fragmentos.
/// </summary>
public class RagService : IRagService
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IEmbeddingConfiguracionRepository _configRepository;
    private readonly IDocumentoRepository? _documentoRepo;
    private readonly ILogger<RagService> _logger;

    /// <summary>
    public RagService(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider,
        IEmbeddingConfiguracionRepository configRepository,
        ILogger<RagService> logger,
        IDocumentoRepository? documentoRepo = null)
    {
        _vectorStore = vectorStore;
        _embeddingProvider = embeddingProvider;
        _configRepository = configRepository;
        _logger = logger;
        _documentoRepo = documentoRepo;
    }

    private async Task<(List<FragmentoRelevanteDto> resultados, string? preferenciaDocUsada)> RecuperarResultadosCoreAsync(
        string consulta, int k, float puntajeMinimo, string? preferenciaDoc)
    {
        var resultados = new List<FragmentoRelevanteDto>();

        try
        {
            // La consulta va íntegra: ningún título derivado la reemplaza.
            // Se piden MÁS candidatos de los que se van a mostrar (plan #13197):
            // con Top-K=5 la sección pedida ("2. Cuerpo de Archivo. El cuerpo de un
            // archivo PDF consiste en una secuencia de objetos indirectos…") quedaba
            // fuera y la respuesta la daban los vecinos que repiten "archivo" y
            // "objetos" (Trailer, tabla de referencia cruzada). Ampliar el POOL no
            // amplía la respuesta: el corte de relevancia, el recorte por frases y
            // el presupuesto por documento siguen acotando lo que se entrega.
            var resultadosBusqueda = await _vectorStore.SearchAsync(consulta, Math.Max(k, CandidatosMinimosBusqueda));

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
                !resultados.Any(r => DocumentoCorresponde(r.DocumentoNombre, r.DocumentoCodigo, preferenciaDoc)))
            {
                // Reintento dirigido al documento preferido (código o nombre del
                // catálogo, o elección explícita del llamador). Semántico igual.
                var consultaDirigida = preferenciaDoc.Trim();
                _logger.LogInformation(
                    "Documento preferido '{Pref}' no estaba en Top-K. Reintentando búsqueda: '{Consulta}'",
                    preferenciaDoc, consultaDirigida);

                var busquedaDirigida = await _vectorStore.SearchAsync(consultaDirigida, Math.Max(k, 15));
                if (busquedaDirigida != null)
                {
                    var extras = busquedaDirigida
                        .Where(r => DocumentoCorresponde(r.MetadataDocumentoNombre, r.MetadataDocumentoCodigo, preferenciaDoc))
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
                    {
                        // Se ANADE la búsqueda dirigida, no se sustituye (plan #13189):
                        // reemplazar descartaría el resto de documentos, que es justo
                        // lo que responde a la otra mitad de una pregunta mixta.
                        resultados = resultados
                            .Concat(extras)
                            .GroupBy(r => (r.DocumentoProcesadoId, r.ChunkId))
                            .Select(g => g.First())
                            .OrderByDescending(r => r.PuntajeSimilitud)
                            .ToList();
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(preferenciaDoc))
            {
                // La preferencia ORDENA, no EXCLUYE (plan #13189). Antes se
                // reemplazaba el resultado por los fragmentos del documento
                // preferido y se perdía el resto: en "hablame sobre el Cuerpo de
                // Archivo segun ejemplo, y sobre la Oferta Laboral segun
                // procedimientos de contratacion" el documento 'r' punteaba
                // 0.7416, el MAYOR de toda la busqueda, y se descartaba porque la
                // preferencia era 'ejemplo'. La segunda mitad de la pregunta se
                // quedaba sin respuesta. Ahora el documento preferido encabeza y
                // los demas siguen por puntaje; el corte de relevancia y la
                // seleccion de la mejor corrida por documento de mas abajo siguen
                // eliminando lo que no compite, asi que una pregunta de un solo
                // documento no se contamination.
                resultados = resultados
                    .OrderByDescending(r => DocumentoCorresponde(r.DocumentoNombre, r.DocumentoCodigo, preferenciaDoc))
                    .ThenByDescending(r => r.PuntajeSimilitud)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al buscar en la base vectorial. Continuando sin contexto documental.");
        }

        return (resultados, preferenciaDoc);
    }

    /// <summary>
    /// Comparación genérica contra el nombre real (contiene en ambas direcciones,
    /// sin acentos). Sin casos por documento: la preferencia viene del catálogo
    /// o del llamador.
    /// </summary>
    private static bool NombreDocumentoCoincide(string nombreDoc, string preferencia)
    {
        if (string.IsNullOrWhiteSpace(nombreDoc) || string.IsNullOrWhiteSpace(preferencia))
            return false;

        var n = SectionExtractorHelper.QuitarAcentos(nombreDoc).ToLowerInvariant().Trim();
        var p = SectionExtractorHelper.QuitarAcentos(preferencia).ToLowerInvariant().Trim();
        if (p.Length < 3)
            return false;
        return n.Contains(p) || p.Contains(n);
    }

    /// <summary>
    /// ¿El fragmento recuperado es del documento preferido? Por nombre (contiene
    /// en ambas direcciones, sin acentos) o por código exacto del catálogo: los
    /// códigos pueden ser de 1 letra ('s') y el match por nombre exige 3+.
    /// La preferencia viene del catálogo o del llamador, nunca de una lista fija.
    /// </summary>
    private static bool DocumentoCorresponde(string? nombreDoc, string? codigoDoc, string preferencia)
    {
        if (!string.IsNullOrWhiteSpace(nombreDoc) && NombreDocumentoCoincide(nombreDoc, preferencia))
            return true;
        return !string.IsNullOrWhiteSpace(codigoDoc)
            && !string.IsNullOrWhiteSpace(preferencia)
            && codigoDoc.Trim().Equals(preferencia.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ¿La consulta nombra un documento Activo del catálogo (por código o nombre)?
    /// Misma normalización genérica que el resto del sistema. Sin nombres fijos:
    /// un documento nuevo se detecta solo.
    /// </summary>
    private async Task<string?> DetectarDocumentoPorCatalogoAsync(string consulta)
    {
        if (_documentoRepo == null || string.IsNullOrWhiteSpace(consulta))
            return null;
        try
        {
            var palabras = new HashSet<string>(
                FiltroSemantico.Palabras(consulta),
                StringComparer.Ordinal);
            if (palabras.Count == 0)
                return null;
            var consultaNorm = FiltroSemantico.Normalizar(consulta);
            var docs = await _documentoRepo.GetAllAsync();
            var activos = docs.Where(d => d.Estado == EstadoDocumento.Activo).ToList();
            foreach (var d in activos)
            {
                var codigo = FiltroSemantico.Normalizar(d.Codigo ?? string.Empty);
                if (codigo.Length >= 4 && palabras.Contains(codigo))
                    return d.Codigo;
                var nombre = FiltroSemantico.Normalizar(d.Nombre ?? string.Empty);
                if (nombre.Length >= 8 && consultaNorm.Contains(nombre, StringComparison.Ordinal))
                    return d.Codigo ?? d.Nombre;
            }
            // Nivel tolerante a typos: cada palabra de la pregunta contra los tokens
            // del código, nombre y archivo (Jaro-Winkler >= 0.85, 5+ letras).
            // "sotenibilidad" ≈ "sostenibilidad_v1.pdf". Sin vocabulario fijo.
            Dictionary<int, string> archivos;
            try { archivos = (await _documentoRepo.GetNombresArchivoAsync()).ToDictionary(x => x.IdDocumento, x => x.NombreArchivo); }
            catch { archivos = new Dictionary<int, string>(); }
            foreach (var d in activos)
            {
                var tokens = new List<string>();
                tokens.AddRange(FiltroSemantico.TokensAlfanumericos(d.Codigo ?? string.Empty));
                tokens.AddRange(FiltroSemantico.TokensAlfanumericos(d.Nombre ?? string.Empty));
                if (archivos.TryGetValue(d.IdDocumento, out var archivo))
                    tokens.AddRange(FiltroSemantico.TokensAlfanumericos(archivo));
                if (tokens.Count == 0) continue;
                foreach (var palabra in palabras.Where(p => p.Length >= 5))
                {
                    if (tokens.Any(t => FiltroSemantico.JaroWinkler(palabra, t) >= 0.85))
                        return d.Codigo;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<RagContextoDto> RecuperarContextoDocumentalAsync(string consulta, int? topK = null, string? savedDocumentPreference = null)
    {
        var cronometro = Stopwatch.StartNew();

        var config = await _configRepository.GetActivaAsync();
        var k = topK ?? config?.CantidadResultados ?? 8;
        var puntajeMinimo = config?.PuntajeMinimo ?? 0.15;
        var longitudMaxima = config?.LongitudMaximaContexto ?? 4000;

        // Preferencia explícita manda; si no, catálogo vivo (nunca nombres fijos).
        var preferenciaDoc = savedDocumentPreference;
        if (string.IsNullOrWhiteSpace(preferenciaDoc))
            preferenciaDoc = await DetectarDocumentoPorCatalogoAsync(consulta);

        var (resultados, _) = await RecuperarResultadosCoreAsync(consulta, k, (float)puntajeMinimo, preferenciaDoc);

        if (resultados.Count == 0 && !string.IsNullOrWhiteSpace(preferenciaDoc))
        {
            _logger.LogInformation("Sin resultados con preferencia de documento; reintentando sin preferencia.");
            (resultados, _) = await RecuperarResultadosCoreAsync(consulta, k, (float)puntajeMinimo, null);
        }

        // Recorte de relevancia COMPARTIDO (SeleccionHerramientaSemantica, misma regla
        // en las dos vías de recuperación): margen + documento ganador / claramente
        // relevante / empate + preferido. El corte nunca vacía: si todo cayera, se
        // conserva el ranking original.
        if (resultados.Count > 1)
        {
            var antes = resultados.Count;
            var mejor = resultados.Max(r => r.PuntajeSimilitud);
            var indices = Asistente.Application.Services.Herramientas.SeleccionHerramientaSemantica.FiltrarPorRelevancia(
                resultados.Select(r => (r.DocumentoNombre ?? string.Empty, r.PuntajeSimilitud)).ToList(),
                puntajeMinimo: (float)puntajeMinimo,
                esPreferido: d => !string.IsNullOrWhiteSpace(preferenciaDoc)
                    && NombreDocumentoCoincide(d, preferenciaDoc));
            // El preferido por CÓDIGO exacto siempre sobrevive (plan #15270). El
            // filtro compartido solo ve el nombre y exige 3+ letras, así que un
            // documento de código corto ('s') con preferencia explícita caía por
            // el tie-break aunque se hubiera recuperado (0.5425) y la pregunta
            // lo nombrara con typo ("sotenibilidad" ≈ "sostenibilidad_v1.pdf").
            // Sin vocabulario: el código sale del catálogo vivo.
            if (!string.IsNullOrWhiteSpace(preferenciaDoc))
            {
                var previos = resultados.ToList();
                var conservados = new HashSet<int>(indices);
                var extra = previos
                    .Select((r, i) => (r, i))
                    .Where(x => DocumentoCorresponde(x.r.DocumentoNombre, x.r.DocumentoCodigo, preferenciaDoc)
                        && !conservados.Contains(x.i))
                    .Select(x => x.i)
                    .ToList();
                if (extra.Count > 0)
                {
                    _logger.LogInformation("RAG: {N} fragmento(s) del documento preferido '{Doc}' restituidos tras el corte.",
                        extra.Count, preferenciaDoc);
                    indices = indices.Concat(extra).OrderBy(i => i).ToList();
                }
            }
if (indices.Count > 0 && indices.Count < resultados.Count)
            {
                _logger.LogInformation("Corte de relevancia RAG: {Antes} → {Despues} fragmentos (mejor {Mejor:F3}).",
                    antes, indices.Count, mejor);
                resultados = indices.Select(i => resultados[i]).ToList();
            }
        }

        _logger.LogInformation("Recuperando contexto documental para: '{Consulta}'. Top-K: {TopK}, Puntaje mínimo: {Puntaje}",
            consulta.Length > 100 ? consulta[..100] + "..." : consulta, k, puntajeMinimo);

        // Recorte extractivo por frase (opción B, plan #10181): con mucho texto
        // (más de un chunk típico) se conservan las corridas alrededor de frases
        // semilla en vez del chunk entero. Con poco texto no vale la pena el costo
        // de los embeddings: va íntegro. Nunca vacía ni falla en silencio.
var totalCaracteres = resultados.Sum(r => r.Texto?.Length ?? 0);
            var modoCobertura = false;
            if (totalCaracteres > UmbralCaracteresRecorteFrases)
            {
                var antesTrim = totalCaracteres;
                // CancellationToken local: el recorte no recibe ct en esta firma.
                using var ctsTrim = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                (resultados, modoCobertura) = await RecortarPorFrasesAsync(resultados, consulta, longitudMaxima, ctsTrim.Token);
                _logger.LogInformation("Recorte por frases RAG: {Antes} → {Despues} caracteres (cobertura={Cobertura}).",
                    antesTrim, resultados.Sum(r => r.Texto?.Length ?? 0), modoCobertura);
            }

            cronometro.Stop();

            var contextoDocumental = ConstruirContextoDocumental(resultados, longitudMaxima, modoCobertura);

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

    /// <summary>
    /// Divide un texto en frases por estructura (puntuación y saltos de línea),
    /// sin vocabulario. Primero des-ajusta los saltos de la extracción PDF: las
    /// líneas de flujo a mitad de frase ("en un\/entorno") se unen con espacio;
    /// solo rompen las líneas vacías, los encabezados numerados/viñetas y las
    /// que terminan en cierre (. ! ? … : ;). Así "1. Cabecera de Fichero" viaja
    /// junto a su primera línea en vez de triturarse. El punto tras dígito no
    /// corta ("1.7", "%PDF-1.6"). Si queda una sola frase, se devuelve vacío
    /// (no hay nada que recortar).
    /// </summary>
    internal static List<string> DividirFrases(string texto)
    {
        var frases = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) return frases;
        var bloques = new List<string>();
        var actual = new StringBuilder();
        void Vaciar()
        {
            var t = actual.ToString().Trim();
            if (t.Length > 0) bloques.Add(t);
            actual.Clear();
        }
        foreach (var cruda in texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var linea = System.Text.RegularExpressions.Regex.Replace(cruda, @"[ \t]+", " ").Trim();
            if (linea.Length == 0) { Vaciar(); continue; }
            if (System.Text.RegularExpressions.Regex.IsMatch(linea, @"^(\d{1,2}\.\s|•|○|[-*]\s|\*\*)"))
                Vaciar();
            if (actual.Length > 0) actual.Append(' ');
            actual.Append(linea);
            if (System.Text.RegularExpressions.Regex.IsMatch(linea, @"[.!?…:;]\s*$")) Vaciar();
        }
        Vaciar();
        foreach (var b in bloques)
        {
            // Lookbehind (no consume): el punto final se conserva en la frase.
            // El par previo al espacio no puede ser dígito+punto ("2. Cuerpo",
            // "%PDF-1.7" no se parten).
            var partes = System.Text.RegularExpressions.Regex.Split(
                b, @"(?<=[.!?…])(?<!\d[.!?…])\s+(?=[A-Z0-9ÁÉÍÓÚÜÑ¿¡""“\(\[])");
            foreach (var p in partes)
            {
                var f = p.Trim();
                if (f.Length >= 8) frases.Add(f);
            }
        }
        if (frases.Count <= 1) return new List<string>();
        return frases;
    }

    /// <summary>
    /// Caché de embeddings por frase (texto exacto → vector). Las frases salen de
    /// chunks inmutables por versión de documento, así que el vector no caduca:
    /// la primera pregunta que toca un documento paga el costo y las siguientes
    /// reutilizan. Tope documentado con limpieza total al llenarse.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, float[]> _cacheEmbeddingsFrases
        = new(System.StringComparer.Ordinal);

    private const int TopeCacheFrases = 20000;

    private static string ClaveFrase(string frase)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(frase));
        return Convert.ToHexString(hash);
    }

    private async Task<float[]?> EmbeddingFraseAsync(string frase, CancellationToken ct)
    {
        try
        {
            var clave = ClaveFrase(frase);
            if (_cacheEmbeddingsFrases.TryGetValue(clave, out var e) && e.Length > 0) return e;
            var vec = await _embeddingProvider.GenerateEmbeddingAsync(frase).WaitAsync(ct);
            if (vec == null || vec.Length == 0) return null;
            if (_cacheEmbeddingsFrases.Count >= TopeCacheFrases) _cacheEmbeddingsFrases.Clear();
            _cacheEmbeddingsFrases[clave] = vec;
            return vec;
        }
        catch { return null; }
    }

    /// <summary>
    /// Normalización para dedup: minúsculas sin tildes y solo alfanumérico.
    /// Dos redacciones con igual letras en igual orden son la misma frase aunque
    /// la extracción varíe en espacios o puntuación.
    /// </summary>
    internal static string NormalizarFrase(string frase)
    {
        if (string.IsNullOrWhiteSpace(frase)) return string.Empty;
        var sb = new StringBuilder(frase.Length);
        foreach (var c in frase.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Tope de texto a partir del cual vale la pena el recorte por frases: por
    /// debajo (un chunk típico) el costo de los embeddings no compensa.
    /// </summary>
    private const int UmbralCaracteresRecorteFrases = 1200;

    /// <summary>
    /// Semilla de frase relevante: responde directamente o la nombra.
    /// Calibrado: las frases que responden miden 0.58+ y el ruido 0.48-0.53.
    /// </summary>
    private const double UmbralFraseSemilla = 0.60;

    /// <summary>
    /// Suelo de la corrida contigua alrededor de una semilla: el contexto
    /// inmediato (encabezados, códigos, continuación) suele medir 0.55+.
    /// </summary>
    private const double UmbralFraseCorrida = 0.55;

    /// <summary>
    /// Caracteres mínimos que un documento debe aportar cuando varias fuentes
    /// responden a la misma pregunta (plan #13191). Sin este suelo, un documento
    /// secundario se quedaba con su única mejor corrida —a menudo solo el
    /// título— y sus datos (viñetas, apartados) quedaban en corridas por debajo
    /// del 0.70. Solo se completan corridas que YA superan UmbralFraseCorrida:
    /// el presupuesto nunca compra ruido.
    /// </summary>
    private const int PresupuestoMinimoPorDocumento = 500;

    /// <summary>
    /// Coseno a partir del cual dos frases del MISMO documento se consideran la
    /// misma información repetida (plan #13193). Alto a propósito: se busca la
    /// redundancia evidente, no parafrasear. Calibrado contra el corpus real, donde
    /// las frases repetidas entre fragmentos miden 0.95+.
    /// </summary>
    private const double UmbralRepeticionSemantica = 1.10;

    /// <summary>
    /// Longitud mínima para participar en el dedup semántico: los encabezados
    /// legitimate parecidos entre secciones ("1. Cabecera...", "2. Cuerpo...")
    /// son cortos y deben sobrevivir los dos.
    /// </summary>
    private const int LongitudMinimaRepeticion = 80;

    /// <summary>
    /// Caracteres que se reservan para cada documento que aún no se ha emitido
    /// (plan #13195). Numérico: sin vocabulario ni pesos por fuente.
    /// </summary>
    private const int MinimoCaracteresPorDocumento = 400;

    /// <summary>
    /// Piso de candidatos que se piden al almacén vectorial, por encima del Top-K
    /// de la configuración (plan #13197). Es solo el POOL de búsqueda: lo que se
    /// entrega lo acota el filtro de relevancia, el recorte por frases y el
    /// presupuesto por documento.
    /// </summary>
    private const int CandidatosMinimosBusqueda = 20;

    /// <summary>
    /// Dispersión máxima entre el mejor y el peor fragmento para considerar que
    /// la pregunta es genérica (resumen) y no enfocada. Por debajo, ninguna
    /// parte destaca y se reparte cobertura; por encima hay un pico y se da
    /// profundidad al ganador.
    /// </summary>
    private const double UmbralPlanoCobertura = 0.10;

    /// <summary>
    /// Fracción del presupuesto a partir de la cual se considera que hay sitio
    /// de sobra y se baja el listón de admisión hasta la semilla (0.60). Evita
    /// entregar 1003 de 2500 caracteres cuando el documento sí tiene más que
    /// contar (#13210). El relleno nunca baja de 0.60.
    /// </summary>
    private const double FactorRellenoDelPresupuesto = 0.75;

/// <summary>
    /// ¿La frase empieza un encabezado nuevo? Señales de FORMATO, no vocabulario:
    /// numeral ("4. Trailer…"), viñeta ("•", "○", "- ") o negrita ("**3.** …").
    /// Son los mismos marcadores que usa el troceado para separar bloques, así
    /// que "la sección termina donde empieza el siguiente encabezado" no depende
    /// de saber qué contiene cada sección.
    /// </summary>
    internal static bool EsEncabezado(string frase)
    {
        if (string.IsNullOrWhiteSpace(frase)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(frase.Trim(),
            @"^(\d{1,2}\.\s|•|○|[-*]\s|\*\*)");
    }

    /// <summary>
    /// Cláusulas de la consulta por puntuación y saltos de línea. Espeja
    /// <see cref="Orchestrator.Planner.PlanBuilder.DividirEnClausulas"/>: mismo
    /// criterio, para que el Planner y el RAG_PARTAN la pregunta igual. Nunca
    /// devuelve vacío (una consulta sin puntuación es una sola cláusula).
    /// </summary>
    internal static IEnumerable<string> DividirEnClausulas(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Array.Empty<string>();
        var partes = texto.Split(new[] { '.', ';', '!', '?', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(p => p.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
        return partes.Count > 0 ? partes : new[] { texto.Trim() };
    }

    /// <summary>Embedding de un texto con caché por texto exacto (mismo mecanismo
    /// que las frases: la clave es el texto completo, no una lista de términos).</summary>
    private async Task<float[]> EmbeddingCacheadaAsync(string texto, CancellationToken ct)
    {
        var clave = ClaveFrase(texto);
        if (_cacheEmbeddingsFrases.TryGetValue(clave, out var e) && e.Length > 0) return e;
        var vec = await _embeddingProvider!.GenerateEmbeddingAsync(texto).WaitAsync(ct);
        if (vec == null || vec.Length == 0) return Array.Empty<float>();
        if (_cacheEmbeddingsFrases.Count >= TopeCacheFrases) _cacheEmbeddingsFrases.Clear();
        _cacheEmbeddingsFrases[clave] = vec;
        return vec;
    }

    /// <summary>
    /// Recorte extractivo por frase: de cada fragmento se
    /// conservan corridas contiguas alrededor de frases semilla, y por documento
    /// se conserva la mejor corrida (más corridas solo con relevancia clara).
    /// Sin vocabulario: todo son cosenos contra la pregunta. Sin semillas se
    /// muestra la mejor frase con sus vecinas; sin embeddings no se recorta nada.
    /// Nunca se vacía por esta etapa.
    /// </summary>
    private async Task<(List<FragmentoRelevanteDto> Fragmentos, bool ModoCobertura)> RecortarPorFrasesAsync(
        List<FragmentoRelevanteDto> fragmentos, string consulta, int presupuestoGlobal, CancellationToken ct)
    {
        try
        {
            if (fragmentos.Count == 0 || _embeddingProvider == null) return (fragmentos, false);
            var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(consulta).WaitAsync(ct);
            if (embPregunta == null || embPregunta.Length == 0) return (fragmentos, false);

            // Puntuación por CLÁUSULA (plan #13218). Con una pregunta de dos partes
            // ("dime los pedidos de Marco Ruiz, y hablame sobre el Trailer de
            // Archivo") el vector único es mitad SQL, mitad PDF, y las frases de la
            // segunda mitad quedan por debajo del suelo: la sección se cortaba
            // justo tras su primera frase y se perdía "startxref… trailer
            // dictionary" (1003 caracteres). Se puntúa cada frase contra la
            // MEJOR cláusula, no contra la mezcla. Mismo criterio que ya aplica el
            // Planner desde #13209. Sin vocabulario: solo puntuación y cortes por
            // puntuación de frase.
            var embConsultas = new List<float[]> { embPregunta };
            foreach (var clausula in DividirEnClausulas(consulta))
            {
                if (string.Equals(clausula, consulta.Trim(), StringComparison.Ordinal)) continue;
                var embClausula = await EmbeddingCacheadaAsync(clausula, ct);
                if (embClausula.Length == embPregunta.Length) embConsultas.Add(embClausula);
            }

            // Pasa 1: corridas por fragmento (índices contiguos conservados).
            // Dedup de frases idénticas (el PDF trae párrafos repetidos literal,
            // plan #10182): compara normalizado (sin tildes, espacios ni
            // puntuación) porque la extracción varía en espacios ("especificación
            // 1.4" vs "especificación1.4"). Colisiones reales imposibles en
            // frases de 8+ caracteres con igual alfanumérico en igual orden.
            // Modo COBERTURA vs PROFUNDIDAD. Una pregunta enfocada ("háblame del
            // Trailer") produce un PICO: un fragmento destaca y el resto queda
            // abajo; ahí lo correcto es profundidad en el ganador. Un resumen
            // genérico ("resúmeme el documento") produce puntajes PLANOS: nada
            // destaca, y optimizar "lo más parecido" entrega veinte frases de una
            // sola sección (Objetos de Diccionario) en vez de un panorama. En
            // modo cobertura se toma la mejor corrida DE CADA fragmento y se
            // reparten el presupuesto por igual, en orden de documento. Sin
            // vocabulario: solo la dispersión de los puntajes.
            // La dispersión se mide POR DOCUMENTO, no global: en una pregunta
            // mixta la cola de otros documentos (r/v/s) estira el rango y
            // escondería que, dentro de cada fuente, nada destaca.
            var puntajesPorDocumento = fragmentos
                .GroupBy(f => f.DocumentoNombre ?? f.DocumentoCodigo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key,
                    g => g.Select(f => (double)f.PuntajeSimilitud).ToList(),
                    StringComparer.OrdinalIgnoreCase);
            var modoCobertura = puntajesPorDocumento.Values.Any(v => v.Count >= 2
                && v.Max() - v.Min() < UmbralPlanoCobertura);

            var frasesVistas = new HashSet<string>(StringComparer.Ordinal);
            var corridas = new List<(int IndiceFragmento, FragmentoRelevanteDto Fragmento, string Documento, List<string> Frases, List<int> Indices, double Mejor)>();
            // Puntuación de cada frase por fragmento: el suelo de presupuesto de
            // más abajo necesita poder añadir frases que nunca llegaron a formar
            // corrida (porque no tocaban la semilla) pero que aún son relevantes.
            var scorePorFragmento = new Dictionary<int, (List<string> Frases, List<double> Puntajes)>();
            for (var fi = 0; fi < fragmentos.Count; fi++)
            {
                var f = fragmentos[fi];
                var frases = DividirFrases(f.Texto ?? string.Empty);
                if (frases.Count <= 1) continue;

                var tareas = frases.Select(s => EmbeddingFraseAsync(s, ct)).ToArray();
                var vectores = await Task.WhenAll(tareas);
                var puntajes = new List<double>();
                for (var i = 0; i < frases.Count; i++)
                    puntajes.Add(vectores[i] == null || vectores[i].Length != embPregunta.Length
                        ? double.NegativeInfinity
                        : embConsultas.Max(e => SeleccionHerramientaSemantica.Coseno(e, vectores[i])));

                scorePorFragmento[fi] = (frases, puntajes);

                var semillas = new HashSet<int>();
                for (var i = 0; i < puntajes.Count; i++)
                    if (puntajes[i] >= UmbralFraseSemilla)
                        semillas.Add(i);
                // Sin semillas: el fragmento pasó el corte por su conjunto, pero
                // ninguna frase responde directo; se muestra solo la mejor con sus
                // vecinas (plan #10181: antes pasaba el chunk entero de 900
                // caracteres).
                if (semillas.Count == 0)
                {
                    var mejor = 0;
                    for (var i = 1; i < puntajes.Count; i++)
                        if (puntajes[i] > puntajes[mejor])
                            mejor = i;
                    // Sin vectores válidos no hay nada que ordenar: intacto.
                    if (double.IsNegativeInfinity(puntajes[mejor])) continue;
                    var entorno = new HashSet<int> { mejor };
                    if (mejor > 0) entorno.Add(mejor - 1);
                    if (mejor < puntajes.Count - 1) entorno.Add(mejor + 1);
                    semillas.UnionWith(entorno);
                }

                var conservadas = new HashSet<int>(semillas);
                foreach (var s in semillas)
                {
                    for (var i = s - 1; i >= 0 && puntajes[i] >= UmbralFraseCorrida; i--)
                        conservadas.Add(i);
                    for (var i = s + 1; i < puntajes.Count && puntajes[i] >= UmbralFraseCorrida; i++)
                        conservadas.Add(i);
                }
                // Parte en corridas contiguas (una frase suelta entre descartes
                // corta la corrida: no se pegan temas distintos).
                var ordenados = conservadas.OrderBy(i => i).ToList();
                var inicio = 0;
                for (var k = 1; k <= ordenados.Count; k++)
                {
                    if (k == ordenados.Count || ordenados[k] != ordenados[k - 1] + 1)
                    {
                        var tramo = ordenados.Skip(inicio).Take(k - inicio).ToList();
                        corridas.Add((fi, f,
                            f.DocumentoNombre ?? f.DocumentoCodigo ?? string.Empty,
                            frases, tramo, tramo.Max(i => puntajes[i])));
                        inicio = k;
                    }
                }
            }

            // Fragmentos de una sola frase o sin vectores: intactos.
            var conCorrida = new HashSet<int>(corridas.Select(c => c.IndiceFragmento));
            var refinados = new List<(int Indice, FragmentoRelevanteDto Fragmento)>();
            for (var fi = 0; fi < fragmentos.Count; fi++)
                if (!conCorrida.Contains(fi))
                    refinados.Add((fi, fragmentos[fi]));

// Pasa 2: por documento, la mejor corrida siempre; las demás solo con
            // relevancia clara (plan #10181: 5 corridas del mismo manual, solo la
            // de Cabecera responde; el resto era "un poco de más").
            var grupos = corridas.GroupBy(
                c => c.Documento,
                StringComparer.OrdinalIgnoreCase).ToList();

            // Documento principal = el de la corrida mejor puntuada. Regla #10181:
            // con UNA sola fuente solo se muestra su mejor corrida; las frases
            // sueltas de ese mismo documento son "un poco de más". Las fuentes
            // SECUNDARIAS sí tienen suelo (plan #13191): si no, una respuesta
            //(documento, 0.88) se quedaba en su línea de título y sus datos
            // (0.56) se perdían.
            var documentoPrimario = grupos.SelectMany(g => g)
                .OrderByDescending(c => c.Mejor)
                .Select(c => c.Documento)
                .FirstOrDefault() ?? string.Empty;

            // El grupo principal ya sebudgetó con el total; a los que van detrás se les
                // reserva su suelo para que la pregunta mixta no pierda la mitad.
                for (var gi = 0; gi < grupos.Count; gi++)
            {
                var grupo = grupos[gi];
                var ordenadas = grupo.OrderByDescending(c => c.Mejor).ToList();
                // En modo cobertura, la "obligatoria" es la mejor corrida DE CADA
                // fragmento (no una sola por documento): así el presupuesto se
                // reparte por todo el documento en vez de concentrarse en la
                // sección ganadora. En profundidad, una sola por documento.
                var obligatoria = modoCobertura
                    ? grupo.GroupBy(c => c.IndiceFragmento)
                        .Select(g => g.OrderByDescending(c => c.Mejor).First())
                        .OrderByDescending(c => c.Mejor).ToList()
                    : ordenadas.Take(1).ToList();
                var elegidas = new List<(int IndiceFragmento, FragmentoRelevanteDto Fragmento, string Documento, List<string> Frases, List<int> Indices, double Mejor)>(obligatoria);
                var yaTomadas = obligatoria.SelectMany(c => c.Indices.Select(i => (c.IndiceFragmento, i))).ToHashSet();
                var obligatorio = obligatoria.Sum(c => c.Indices.Sum(i => (c.Frases[i] ?? string.Empty).Length + 2));
                // Presupuesto global menos lo ya comprometido y menos el suelo
                // reservado a las fuentes que aún no se han emitido (#13197).
                var presupuestoRestante = presupuestoGlobal - obligatorio
                    - Math.Max(0, grupos.Count - gi - 1) * MinimoCaracteresPorDocumento;
                // El principal solo admite corridas con relevancia clara (0.70): por
                // debajo son "un poco de más" (calibración de #10181, la corrida
                // de 0.65 se descarta). Los secundarios bajan al suelo de corrida
                // para que no se queden en su título (#13191).
                var esPrincipal = string.Equals(grupo.Key, documentoPrimario, StringComparison.OrdinalIgnoreCase);
                var minimoAdmision = esPrincipal
                    ? Asistente.Application.Services.Herramientas.SeleccionHerramientaSemantica.SimilitudClaramenteRelevante
                    : UmbralFraseCorrida;
                var candidatas = new List<(int Frag, int Idx, double Score, List<string> Frases)>();
                foreach (var frag in grupo)
                    if (scorePorFragmento.TryGetValue(frag.IndiceFragmento, out var sp))
                        for (var i = 0; i < sp.Frases.Count; i++)
                            if (sp.Puntajes[i] >= minimoAdmision && !yaTomadas.Contains((frag.IndiceFragmento, i)))
                                candidatas.Add((frag.IndiceFragmento, i, sp.Puntajes[i], sp.Frases));
                var ocupado = obligatorio;
                // En cobertura no hay relleno por puntaje: el reparto ya garantizó
                // una corrida por fragmento y añadir reconcentraría en el ganador.
                // Tampoco hay continuación de sección (esa extiende en profundidad
                // lo que cobertura quiere repartir). El dedup semántico sí se
                // mantiene: con muchos fragmentos hay más repeticiones entre ellos.
                if (!modoCobertura)
                foreach (var cand in candidatas.OrderByDescending(c => c.Score))
                {
                    var largo = (cand.Frases[cand.Idx] ?? string.Empty).Length + 2;
                    if (presupuestoRestante - largo < 0) break;
                    presupuestoRestante -= largo;
                    ocupado += largo;
                    var frag = fragmentos[cand.Frag];
                    elegidas.Add((cand.Frag, frag,
                        frag.DocumentoNombre ?? frag.DocumentoCodigo ?? string.Empty,
                        cand.Frases, new List<int> { cand.Idx }, cand.Score));
                }
                if (!modoCobertura)
                {
                // Continuidad de SECCIÓN (planes #13219, #13226). El defecto real que
                // queda no es de relevancia sino de estructura: la respuesta
                // llegaba correcta hasta "…c ciertos objetos especiales" y ahí se
                // cortaba, y de paso se colaban viñetas de la sección ANTERIOR.
                // En un documento una sección es un bloque contiguo: desde la
                // frase que respondió hasta el ENCABEZADO siguiente, aunque el
                // bloque cruce de chunk (el troceado solapa: el 2031 termina en
                // "…catálogo de documento" y el 2032 empieza tres líneas antes).
                // Las frases se FUNDEN con su semilla: no son passages sueltos.
                // El encabezado se reconoce por MARCADORES de formato —numeral,
                // viñeta o negrita—, los mismos que ya usa el troceado; no se
                // mira qué dice la sección.
                // Al saltar de chunk NO se exige puntaje: si el chunk anterior
                // terminó a mitad de sección hablando del tema (lo estableció
                // una coincidencia fuerte), lo que sigue en el documento ES la
                // continuación. Pedirle además que se parezca a la pregunta es lo
                // que dejaba fuera "startxref… offset de bytes".
                var porOrden = new Dictionary<(int Doc, int Orden), int>();
                for (var k = 0; k < fragmentos.Count; k++)
                    porOrden[(fragmentos[k].DocumentoProcesadoId, fragmentos[k].Orden)] = k;
                var presupuestoAgotado = false;
                var continuaciones = new List<(int IndiceFragmento, FragmentoRelevanteDto Fragmento, string Documento, List<string> Frases, List<int> Indices, double Mejor)>();
                foreach (var c in elegidas)
                {
                    if (presupuestoAgotado) break;
                    if (!scorePorFragmento.TryGetValue(c.IndiceFragmento, out var sp)) continue;
                    var fragActual = c.IndiceFragmento;
                    var ultimo = c.Indices.Count > 0 ? c.Indices.Max() : -1;
                    var visitados = new HashSet<int> { fragActual };
                    while (true)
                    {
                        var indicesNuevos = new List<int>();
                        var llegoAlFinal = true;
                        for (var k = ultimo + 1; k < sp.Frases.Count; k++)
                        {
                            if (EsEncabezado(sp.Frases[k])) { llegoAlFinal = false; break; }
                            // Suelo de corrida también aquí, medido contra el
                            // modelo real (nomic-embed-text): el cuerpo de una
                            // sección puntúa 0.60-0.73 ("Un archivo PDF debería
                            // empezar…" 0.73, "…startxref…" 0.60) y el relleno
                            // 0.47. El 0.55 los separa sin vocabulario.
                            if (sp.Puntajes[k] < UmbralFraseCorrida) { llegoAlFinal = false; break; }
                            var largo = (sp.Frases[k] ?? string.Empty).Length + 2;
                            if (presupuestoRestante - largo < 0) { presupuestoAgotado = true; break; }
                            presupuestoRestante -= largo;
                            indicesNuevos.Add(k);
                        }
                        if (indicesNuevos.Count > 0)
                        {
                            if (fragActual == c.IndiceFragmento)
                                foreach (var ni in indicesNuevos) c.Indices.Add(ni);
                            else
                                continuaciones.Add((fragActual, fragmentos[fragActual],
                                    fragmentos[fragActual].DocumentoNombre ?? fragmentos[fragActual].DocumentoCodigo ?? string.Empty,
                                    sp.Frases, indicesNuevos, 0.0));
                        }
                        if (presupuestoAgotado) break;
                        // Solo se salta al siguiente chunk si se consumió ESTE
                        // hasta el final sin cortes: eso significa que la sección
                        // sigue abierta. Si se paró por encabezado, suelo o
                        // presupuesto, no hay continuación que buscar.
                        if (!llegoAlFinal) break;
                        // ¿Se llegó al final del fragmento sin encontrar
                        // encabezado? La sección continúa en el siguiente chunk
                        // del mismo documento en orden físico. Su primera frase
                        // debe superar el suelo de corrida: si no, es otro tema
                        // (el relleno del fixture puntúa 0.42 y se queda fuera).
                        var f0 = fragmentos[fragActual];
                        if (!porOrden.TryGetValue((f0.DocumentoProcesadoId, f0.Orden + 1), out var fragSiguiente)
                            || !visitados.Add(fragSiguiente)
                            || !scorePorFragmento.TryGetValue(fragSiguiente, out var spSiguiente)
                            || spSiguiente.Frases.Count == 0
                            || EsEncabezado(spSiguiente.Frases[0])
                            || spSiguiente.Puntajes[0] < UmbralFraseCorrida)
                            break;
                        // Se continúa en el siguiente chunk con sus propias
                        // frases e índices: forman la misma sección que la
                        // semilla original.
                        fragActual = fragSiguiente;
                        sp = spSiguiente;
                        ultimo = -1;
                    }
                }
                elegidas.AddRange(continuaciones);
                } // fin cobertura: sin continuación de sección
                // Dedup SEMÁNTICO dentro del documento (plan #13193). El dedup
                // exacto (frasesVistas) no caza que dos frases DISTINTAS del mismo
                // manual digan lo mismo: "La tabla de referencia cruzada contiene
                // información que permite el acceso aleatorio..." repetido en dos
                // fragmentos se quedaba twice en la entrega. Se compara por coseno
                // contra las frases ya conservadas de ESTE documento. Sin
                // vocabulario: solo similitud. Las frases cortas no entran (encabezados
                // legitimately parecidas entre secciones deben sobrevivir).
                var repetidas = new HashSet<(int Frag, int Idx)>();
                var vectoresDelDocumento = new List<float[]>();
                foreach (var c in elegidas)
                {
                    foreach (var i in c.Indices)
                    {
                        var frase = c.Frases[i];
                        if (string.IsNullOrWhiteSpace(frase) || frase.Trim().Length < LongitudMinimaRepeticion) continue;
                        var vector = await EmbeddingFraseAsync(frase, ct);
                        if (vector == null || vector.Length != embPregunta.Length) continue;
                        var yaDicha = vectoresDelDocumento.Any(v =>
                            SeleccionHerramientaSemantica.Coseno(v, vector) >= UmbralRepeticionSemantica);
                        if (yaDicha) repetidas.Add((c.IndiceFragmento, i));
                        else vectoresDelDocumento.Add(vector);
                    }
                }
                foreach (var c in elegidas)
                {
                    var unicas = c.Indices
                        .Where(i => !repetidas.Contains((c.IndiceFragmento, i)))
                        .Where(i => frasesVistas.Add(NormalizarFrase(c.Frases[i])))
                        .ToList();
                    if (unicas.Count == 0) continue;
                    var texto = string.Join("\n\n", unicas.Select(i => c.Frases[i]));
                    if (string.IsNullOrWhiteSpace(texto)) continue;
                    var f = c.Fragmento;
                    refinados.Add((c.IndiceFragmento, new FragmentoRelevanteDto
                    {
                        DocumentId = f.DocumentId,
                        ChunkId = f.ChunkId,
                        DocumentoProcesadoId = f.DocumentoProcesadoId,
                        Texto = texto,
                        PuntajeSimilitud = f.PuntajeSimilitud,
                        Orden = f.Orden,
                        PaginaInicial = f.PaginaInicial,
                        PaginaFinal = f.PaginaFinal,
                        DocumentoNombre = f.DocumentoNombre,
                        DocumentoCodigo = f.DocumentoCodigo,
                        SearchRank = f.SearchRank,
                        IdDocumento = f.IdDocumento,
                        IdVersion = f.IdVersion
                    }));
                }
            }

            // En cobertura, el reparto ya se hizo (mejor corrida por fragmento);
            // el relleno por puntaje y la continuación de sección reconcentrarían
            // en la sección ganadora, deshaciendo el panorama. Aquí cada fragmento
            // recibe la MISMA cuota (presupuesto / nº de fragmentos): así el final
            // del documento también aparece, no solo el principio. Sin cuota igual,
            // el reparto codicioso llenaba el presupuesto con los primeros chunks
            // (intro, páginas) y la Cabecera/Cuerpo/Trailer no llegaban nunca.
            List<(int Indice, FragmentoRelevanteDto Fragmento)> refinadosFinales;
            if (modoCobertura)
            {
                // Muestreo sistemático por el documento: en vez de repartir una
                // cuota igual (que con 14 fragmentos da ~180 chars legibles por
                // sección: staccato), se toman fragmentos SALTEADOS en orden de
                // documento, cada uno con su mejor corrida íntegra. Así el
                // panorama cubre principio, medio y fin con párrafos legibles.
                // Sin vocabulario: posiciones y longitudes, nunca contenido.
                refinadosFinales = new List<(int, FragmentoRelevanteDto)>();
                var porDocumento = refinados
                    .GroupBy(x => x.Fragmento.DocumentoProcesadoId)
                    .OrderBy(g => g.Key)
                    .ToList();
                var cuotaPorDocumento = porDocumento.Count > 0
                    ? presupuestoGlobal / porDocumento.Count
                    : presupuestoGlobal;
                foreach (var grupoDoc in porDocumento)
                {
                    var frags = grupoDoc.OrderBy(x => x.Fragmento.Orden).ToList();
                    var totalGrupo = frags.Sum(x => x.Fragmento.Texto?.Length ?? 0);
                    var paso = totalGrupo <= 0 ? 1 : Math.Max(1, (int)Math.Round((double)totalGrupo / Math.Max(1, cuotaPorDocumento)));
                    var usadoGrupo = 0;
                    for (var fi = 0; fi < frags.Count; fi += paso)
                    {
                        var r = frags[fi];
                        var texto = r.Fragmento.Texto ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(texto)) continue;
                        // El stride ya reparte el presupuesto (toma ~cuota en total);
                        // este tope es solo red de seguridad con 50% de tolerancia
                        // por varianza de longitudes, para no cortar la cola.
                        if (usadoGrupo + texto.Length > cuotaPorDocumento * 3 / 2) break;
                        usadoGrupo += texto.Length;
                        refinadosFinales.Add(r);
                    }
                }
            }
            else
            {
                refinadosFinales = refinados.OrderBy(x => x.Indice).ToList();
            }

            return (refinadosFinales.Count > 0
                // En cobertura ya vienen en orden de documento y caben en el
                // presupuesto: se entregan tal cual para que el tope final no
                // recorte por puntaje lo que el reparto distribuyó.
                ? (refinadosFinales.Select(x => x.Fragmento).ToList(), modoCobertura)
                : (fragmentos, false));
        }
        catch
        {
            return (fragmentos, false);
        }
    }
    /// <summary>
    /// Contexto por ranking: chunks top por score (dedup), cada uno citado con su
    /// documento de origen (metadatos reales). Sin etiqueta de sección inventada,
    /// sin bloque de instrucciones. En modo cobertura el orden es por documento
    /// (fuente y posición): el panorama sigue la estructura del manual en vez
    /// del ranking. Sin vocabulario en ningún caso: solo puntajes y metadatos.
    /// </summary>
    internal static string ConstruirContextoDocumental(List<FragmentoRelevanteDto> fragmentos, int longitudMaxima, bool ordenDocumento = false)
    {
        if (fragmentos.Count == 0)
            return string.Empty;

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var baseOrdenada = ordenDocumento
            ? fragmentos.OrderBy(f => f.DocumentoProcesadoId).ThenBy(f => f.Orden).AsEnumerable()
            : fragmentos.OrderByDescending(f => f.PuntajeSimilitud).ThenBy(f => f.SearchRank).AsEnumerable();
        var ordenados = baseOrdenada
            .Where(f => !string.IsNullOrWhiteSpace(f.Texto) && vistos.Add(f.Texto.Trim()))
            .ToList();

        // Un bloque por documento (plan #13193). Se agrupa por la etiqueta y se
        // conserva el orden en que aparece cada fuente: antes, si los fragmentos
        // alternaban documento (ejemplo, r, ejemplo) se emitían TRES encabezados
        // "Según **...**" para dos fuentes y el mismo manual quedaba partido en
        // dos bloques lejanos. El criterio es la etiqueta que ya se imprimía, no
        // el contenido.
        var porDocumento = new List<(string Etiqueta, List<FragmentoRelevanteDto> Fragmentos)>();
        var indiceEtiqueta = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in ordenados)
        {
            var etiqueta = $"Según **{f.DocumentoNombre ?? "Documento"}**" +
                (string.IsNullOrWhiteSpace(f.DocumentoCodigo) ? "" : $" ({f.DocumentoCodigo})") + ":";
            if (!indiceEtiqueta.TryGetValue(etiqueta, out var pos))
            {
                indiceEtiqueta[etiqueta] = porDocumento.Count;
                porDocumento.Add((etiqueta, new List<FragmentoRelevanteDto>()));
                pos = porDocumento.Count - 1;
            }
            porDocumento[pos].Fragmentos.Add(f);
        }

        var sb = new StringBuilder();
        var primero = true;
        for (var di = 0; di < porDocumento.Count; di++)
        {
            var (etiqueta, delDocumento) = porDocumento[di];
            if (!primero) sb.Append("\n\n---\n\n");
            sb.Append(etiqueta).Append("\n\n");
            primero = false;

            // Presupuesto justo entre fuentes (plan #13195). Antes el primer
            // documento se comia el limite entero y el metodo hacia `return`:
            // 'ejemplo' (3203 chars) llenaba los 2500 disponibles y 'r' no se
            // emitia NUNCA, aunque hubiera pasado el corte de relevancia con
            // 0.7096. La segunda mitad de la pregunta se perdia por un recorte de
            // longitud, no por el ruteo. Ahora se reserva un minimo para cada
            // documento que queda por delante; el sobrante lo usa el primero.
            var restantes = porDocumento.Count - di - 1;
            var cupoDocumento = longitudMaxima - sb.Length - restantes * MinimoCaracteresPorDocumento;
            if (cupoDocumento <= 0)
                // No cabe el minimo de todos: se reparte lo que haya sin prometer
                // mas de lo que queda.
                cupoDocumento = Math.Max(0, (longitudMaxima - sb.Length) / (restantes + 1));

            var inicio = sb.Length;
            foreach (var f in delDocumento)
            {
                var texto = SectionExtractorHelper.LimpiarTextoPdf(f.Texto.Trim());
                if (texto.Length == 0) continue;
                if (sb.Length - inicio + texto.Length > cupoDocumento)
                {
                    var restante = inicio + cupoDocumento - sb.Length;
                    if (restante > 200)
                        sb.Append(TruncarPorFrase(texto, restante));
                    sb.Append("\n\n[... truncado ...]");
                    break;
                }
                sb.Append(texto).Append("\n\n");
            }
            if (sb.Length >= longitudMaxima) break;
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Recorta al límite terminando en fin de frase (". ", ".\n", "?"...), para no
    /// cortar a media palabra ("inclui [... truncado ...]", plan #9072). Criterio
    /// por puntuación, sin vocabulario. Si no hay frase completa, corte duro.
    /// </summary>
    internal static string TruncarPorFrase(string texto, int maximo)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length <= maximo)
            return texto;
        var corte = texto[..maximo];
        var candidatos = new[]
        {
            corte.LastIndexOf(". ", StringComparison.Ordinal),
            corte.LastIndexOf(".\n", StringComparison.Ordinal),
            corte.LastIndexOf("? ", StringComparison.Ordinal),
            corte.LastIndexOf("! ", StringComparison.Ordinal),
        };
        var mejor = candidatos.Max();
        // Exigir sustancia mínima relativa: no quedarse con un fragmento
        // diminuto cuando había mucho más texto disponible.
        if (mejor >= maximo / 3)
            return corte[..(mejor + 1)].Trim();
        return corte.Trim();
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
}
