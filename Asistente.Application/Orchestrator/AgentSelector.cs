using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Orchestrator;

/// <summary>
/// Implementación de IAgentSelector (RF Actividad 2).
/// Selecciona agentes colaboradores según:
///  - Tipo de solicitud: SIMILITUD SEMÁNTICA contra la descripción real de cada
///    herramienta (dato de configuración), no contra un vocabulario fijo.
///  - Capacidades (herramientas asignadas al agente).
///  - Permisos (reglas de colaboración desde el agente principal).
///  - Prioridad y límites de profundidad/max agentes.
///
/// SIN KEYWORDS: ninguna decisión se toma por substring sobre el texto del usuario.
/// El vocabulario vive en Herramientas.Descripcion / Categoria, que son datos: una base
/// de datos nueva se agrega configurando herramientas, sin tocar este archivo.
/// </summary>
public class AgentSelector : IAgentSelector
{
    private readonly IAsistenteRepository _asistenteRepository;
    private readonly IAgentCollaborationRuleRepository _reglasRepo;
    private readonly IAutorizacionService _autorizacion;
    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly IHerramientaRepository? _herramientaRepository;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, float[]> _descCache = new();
    private static readonly SemaphoreSlim _descLock = new(1, 1);

    /// <summary>Umbral de similitud para considerar que una herramienta es la adecuada.</summary>
    private const double UmbralSimilitud = 0.45;

    public AgentSelector(
        IAsistenteRepository asistenteRepository,
        IAgentCollaborationRuleRepository reglasRepo,
        IAutorizacionService autorizacion,
        IEmbeddingProvider? embeddingProvider = null,
        IHerramientaRepository? herramientaRepository = null)
    {
        _asistenteRepository = asistenteRepository;
        _reglasRepo = reglasRepo;
        _autorizacion = autorizacion;
        _embeddingProvider = embeddingProvider;
        _herramientaRepository = herramientaRepository;
    }

    public async Task<IEnumerable<AgentCandidate>> SelectAgentsAsync(
        AgentRequest request, CancellationToken cancellationToken = default)
    {
        var principal = await _asistenteRepository.GetByIdAsync(request.IdAgentePrincipal);
        if (principal == null) return new List<AgentCandidate>();

        var todos = (await _asistenteRepository.GetAllAsync())
            .Where(a => a.Activo && a.IdAsistente != request.IdAgentePrincipal)
            .ToList();

        // Carga determinista de herramientas por agente (AsNoTracking), independiente del
        // Include de GetAllAsync y del identity-map del DbContext compartido.
        var toolCodes = new Dictionary<int, HashSet<string>>();
        foreach (var a in todos)
        {
            var codigos = await _asistenteRepository.GetHerramientasActivasAsync(a.IdAsistente);
            toolCodes[a.IdAsistente] = codigos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // Herramientas activas con su DESCRIPCIÓN (dato de configuración). El enrutamiento
        // se hace contra estas descripciones, no contra un vocabulario escrito en código.
        var catalogoHerramientas = await CargarCatalogoHerramientasAsync();

        var candidatos = new List<AgentCandidate>();

        // Si el usuario sugirió agentes explícitamente, respetarlos (sujeos a reglas/permisos).
        var sugeridos = request.AgentesSugeridos?.ToHashSet() ?? new HashSet<int>();

        foreach (var agente in todos)
        {
            var score = 0.0;
            var rol = "Colaborador";
            var instruccionRol = string.Empty;
            string? alcance = null;
            var depende = new List<int>();

            var tools = toolCodes.GetValueOrDefault(agente.IdAsistente, new HashSet<string>());

            // --- Intención = herramienta cuya descripción real se parece más a la pregunta ---
            // Se evalúan SOLO las herramientas que este agente tiene asignadas: la
            // capacidad se combina con la pertenencia, sin listas de palabras.
            var mejorHerramienta = await MejorHerramientaAsync(
                request.Pregunta, tools, catalogoHerramientas, cancellationToken);

            if (mejorHerramienta is not null)
            {
                score += mejorHerramienta.Similitud;
                rol = mejorHerramienta.Categoria;
                instruccionRol = mejorHerramienta.Descripcion;
                alcance = AlcanceDeCapacidad(mejorHerramienta.Codigo);
                // Una herramienta que CONSUME lo producido por otras (una reporte) no
                // puede correr antes que ellas. Se declara por configuración, no por
                // nombre de herramienta.
                if (mejorHerramienta.ConsumeDatosPrevios)
                    depende.Add(request.IdAgentePrincipal);
            }

            if (sugeridos.Contains(agente.IdAsistente))
                score += 5;

            if (score <= 0) continue;

            // --- Permisos: regla de colaboración desde el agente principal ---
            var permitido = await _reglasRepo.EstaPermitidoAsync(request.IdAgentePrincipal, agente.IdAsistente, cancellationToken);
            if (!permitido) continue; // Regla 3: solo autorizados por regla de colaboración

            // Nota: la colaboración multi-agente se rige por AgentCollaborationRule (Regla 3),
            // no por la asignación directa de asistentes al usuario (que aplica al chat 1-a-1).
            // Por eso NO se filtra aquí por _autorizacion.VerificarAsistenteAsync.

            candidatos.Add(new AgentCandidate
            {
                IdAgente = agente.IdAsistente,
                Nombre = agente.Nombre,
                Objetivo = agente.Objetivo ?? "",
                Rol = rol,
                InstruccionRol = instruccionRol,
                Alcance = alcance,
                Prioridad = agente.Version,
                DependeDe = depende.Distinct().ToList(),
                Puntuacion = score
            });
        }

        // Ordenar por puntuación descendente y limitar a los más relevantes.
        return candidatos
            .OrderByDescending(c => c.Puntuacion)
            .ThenBy(c => c.IdAgente)
            .ToList();
    }

    private static bool AgenteTieneHerramienta(Asistente.Domain.Entities.Asistente agente, string codigo)
        => agente.AsistentesHerramientas.Any(h => h.Activa &&
            (h.Herramienta?.Codigo?.Equals(codigo, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>
    /// Herramienta candidata: descripción y categoría vienen de la BD (configuración).
    /// </summary>
    private sealed record HerramientaCandidata(
        string Codigo, string Descripcion, string Categoria, bool ConsumeDatosPrevios);

    /// <summary>Herramienta elegida para un agente, con su puntaje de similitud.</summary>
    private sealed record MatchHerramienta(
        string Codigo, string Descripcion, string Categoria, bool ConsumeDatosPrevios, double Similitud);

    /// <summary>
    /// Catálogo de herramientas activas con su descripción. Es el "vocabulario" del
    /// enrutamiento y vive en la base: agregar una capacidad no obliga a recompilar.
    /// Si el repositorio no está disponible se cae a las herramientas que ya vienen
    /// en los asistentes (descripción vacía → sin enrutar por similitud).
    /// </summary>
    private async Task<Dictionary<string, HerramientaCandidata>> CargarCatalogoHerramientasAsync()
    {
        var catalogo = new Dictionary<string, HerramientaCandidata>(StringComparer.OrdinalIgnoreCase);
        if (_herramientaRepository == null) return catalogo;
        try
        {
            foreach (var h in await _herramientaRepository.GetActivasAsync())
            {
                if (string.IsNullOrWhiteSpace(h.Codigo) || string.IsNullOrWhiteSpace(h.Descripcion)) continue;
                catalogo[h.Codigo] = new HerramientaCandidata(
                    h.Codigo, h.Descripcion.Trim(),
                    string.IsNullOrWhiteSpace(h.Categoria) ? "Utilidad" : h.Categoria.Trim(),
                    // Capacidad que consume lo producido por otras (una reporte): se
                    // declara por configuración, no por el nombre de la herramienta.
                    EsCategoriaConsumidora(h.Categoria));
            }
        }
        catch { /* sin catálogo: sin enrutamiento por similitud */ }
        return catalogo;
    }

    /// <summary>
    /// ¿La categoría declara que consume datos de otras capacidades? Se decide por el
    /// identificador de categoría configurado, no por el nombre de una herramienta ni
    /// por palabras del texto del usuario.
    /// </summary>
    private static bool EsCategoriaConsumidora(string? categoria)
        => string.Equals(categoria?.Trim(), CategoriaConsumidora, StringComparison.OrdinalIgnoreCase);

    /// <summary>Categoría (dato de configuración) de las herramientas que consolidan resultados.</summary>
    private const string CategoriaConsumidora = "Reporte";

    /// <summary>
    /// Ámbito de recuperación de las capacidades integradas. Son los ÚNICOS dos
    /// behaviours de recuperación del sistema (documentos / base de datos) y se
    /// declaran por el código de la herramienta registrada, no por vocabulario del
    /// negocio ni por palabras de la pregunta. Una capacidad nueva nace con ámbito
    /// null (sin recuperación acotada) hasta que se le declare uno.
    /// </summary>
    private static string? AlcanceDeCapacidad(string codigoHerramienta) => codigoHerramienta switch
    {
        var c when c.Equals("DocumentSearchTool", StringComparison.OrdinalIgnoreCase) => "documental",
        var c when c.Equals("SqlQueryTool", StringComparison.OrdinalIgnoreCase) => "datos",
        _ => null
    };

    /// <summary>
    /// Herramienta del agente cuya DESCRIPCIÓN se parece más a la pregunta. Solo se
    /// consideran las que el agente tiene asignadas (pertenencia, no vocabulario) y solo
    /// si superan el umbral: sin embeddings disponibles devuelve null y el agente
    /// participa como colaborador genérico, nunca por una adivinanza de palabras.
    /// </summary>
    private async Task<MatchHerramienta?> MejorHerramientaAsync(
        string pregunta, HashSet<string> codigosAgente,
        Dictionary<string, HerramientaCandidata> catalogo, CancellationToken ct)
    {
        if (_embeddingProvider == null || codigosAgente.Count == 0 || catalogo.Count == 0) return null;

        var candidatas = codigosAgente
            .Where(c => catalogo.ContainsKey(c))
            .Select(c => catalogo[c])
            .ToList();
        if (candidatas.Count == 0) return null;

        // Ollama en CPU puede tardar minutos por embedding: tope propio o la
        // selección colgaría la ejecución (sin este límite, EnProceso eterno).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));
        var ctt = timeoutCts.Token;
        try
        {
            var embPregunta = await _embeddingProvider.GenerateEmbeddingAsync(pregunta).WaitAsync(ctt);
            var mejor = (Codigo: (string?)null, Similitud: -1.0);
            foreach (var h in candidatas)
            {
                var embDesc = await EmbeddingCacheadoAsync(h.Descripcion, ctt);
                var sim = Coseno(embPregunta, embDesc);
                if (sim > mejor.Similitud) mejor = (h.Codigo, sim);
            }

            if (mejor.Codigo is null || mejor.Similitud < UmbralSimilitud) return null;
            var elegida = catalogo[mejor.Codigo];
            return new MatchHerramienta(elegida.Codigo, elegida.Descripcion, elegida.Categoria,
                                        elegida.ConsumeDatosPrevios, mejor.Similitud);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // apagado real: propagar, no enmascarar
        }
        catch
        {
            // Sin embeddings no se inventa la intención: el agente queda como
            // colaborador genérico (antes aquí entraban las keywords).
            return null;
        }
    }

    private async Task<float[]> EmbeddingCacheadoAsync(string texto, CancellationToken ct)
    {
        if (_descCache.TryGetValue(texto, out var e)) return e;
        await _descLock.WaitAsync(ct);
        try
        {
            if (_descCache.TryGetValue(texto, out e)) return e;
            e = await _embeddingProvider!.GenerateEmbeddingAsync(texto).WaitAsync(ct);
            _descCache[texto] = e;
            return e;
        }
        finally { _descLock.Release(); }
    }

    private static double Coseno(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++) { dot += (double)a[i] * b[i]; na += (double)a[i] * a[i]; nb += (double)b[i] * b[i]; }
        if (na <= 0 || nb <= 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
