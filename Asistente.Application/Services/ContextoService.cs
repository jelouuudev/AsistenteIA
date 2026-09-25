using System.Diagnostics;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class ContextoService
{
    private readonly IConfiguracionMemoriaRepository _configRepo;
    private readonly IMensajeRepository _mensajeRepository;
    private readonly ILogger<ContextoService> _logger;

    public ContextoService(
        IConfiguracionMemoriaRepository configRepo,
        IMensajeRepository mensajeRepository,
        ILogger<ContextoService> logger)
    {
        _configRepo = configRepo;
        _mensajeRepository = mensajeRepository;
        _logger = logger;
    }

    public virtual async Task<ContextoConstruido> ConstruirContextoAsync(
        Conversacion conversacion,
        Mensaje nuevoMensaje,
        CancellationToken cancellationToken = default)
    {
        var cronometro = Stopwatch.StartNew();

        var config = await _configRepo.GetActivaAsync();
        var maxMensajes = config?.MaximoMensajesContexto ?? 20;
        // Tope de tokens (antes no se leia: parametro muerto). Estimacion: 1 token ~= 4 caracteres.
        var maxTokens = Math.Max(50, config?.MaximoTokensContexto ?? 4096);

        var historial = (await _mensajeRepository.GetByConversacionIdAsync(conversacion.IdConversacion))
            .ToList();

        var mensajesRecientes = new List<Mensaje>();
        var resumenUtilizado = conversacion.ResumenContexto;

        if (historial.Count > maxMensajes && !string.IsNullOrWhiteSpace(conversacion.ResumenContexto))
        {
            mensajesRecientes.AddRange(historial.TakeLast(maxMensajes));
        }
        else
        {
            mensajesRecientes.AddRange(historial);
        }

        mensajesRecientes.Add(nuevoMensaje);

        // Recorte por tokens estimados: quita los mas antiguos hasta entrar en el limite.
        // Siempre conserva al menos el mensaje nuevo y el resumen.
        var tokensVentana = CalcularTokensEstimados(mensajesRecientes, conversacion.ResumenContexto);
        while (tokensVentana > maxTokens && mensajesRecientes.Count > 1)
        {
            mensajesRecientes.RemoveAt(0);
            tokensVentana = CalcularTokensEstimados(mensajesRecientes, conversacion.ResumenContexto);
        }
        if (mensajesRecientes.Count < historial.Count + 1)
            _logger.LogInformation("Contexto recortado por tokens: {Total} -> {Ventana} mensajes ({Tokens} tokens estimados, tope {Tope}).",
                historial.Count + 1, mensajesRecientes.Count, tokensVentana, maxTokens);

        cronometro.Stop();

        var tokensEstimados = tokensVentana;

        return new ContextoConstruido
        {
            MensajesRecientes = mensajesRecientes,
            ResumenContexto = resumenUtilizado,
            CantidadMensajesEnviados = mensajesRecientes.Count,
            CantidadTokensEstimados = tokensEstimados,
            TiempoConstruccionMs = cronometro.ElapsedMilliseconds
        };
    }

    public async Task<string> GenerarResumenAsync(Conversacion conversacion)
    {
        var config = await _configRepo.GetActivaAsync();
        var longitudResumen = config?.LongitudResumen ?? 500;

        var historial = (await _mensajeRepository.GetByConversacionIdAsync(conversacion.IdConversacion))
            .OrderBy(m => m.FechaHora)
            .ToList();

        if (historial.Count == 0)
        {
            _logger.LogWarning("No hay mensajes para generar resumen en conversación {Id}", conversacion.IdConversacion);
            return string.Empty;
        }

        _logger.LogInformation("Generando resumen para conversación {Id}. Mensajes: {Total}",
            conversacion.IdConversacion, historial.Count);

        return GenerarResumenFallback(historial, longitudResumen);
    }

    public async Task<string> GenerarResumenAsync(
        Conversacion conversacion,
        IOllamaService ollamaService,
        CancellationToken cancellationToken = default)
    {
        var config = await _configRepo.GetActivaAsync();
        var longitudResumen = config?.LongitudResumen ?? 500;

        var historial = (await _mensajeRepository.GetByConversacionIdAsync(conversacion.IdConversacion))
            .OrderBy(m => m.FechaHora)
            .ToList();

        if (historial.Count == 0)
        {
            _logger.LogWarning("No hay mensajes para generar resumen en conversación {Id}", conversacion.IdConversacion);
            return string.Empty;
        }

        var mensajesParaResumir = Math.Min(historial.Count, 20);
        var contenidoHistorial = string.Join("\n", historial.Take(mensajesParaResumir).Select(m =>
            $"{(m.Rol == RolMensaje.User ? "Usuario" : "Asistente")}: {m.Contenido}"));

        _logger.LogInformation("Generando resumen para conversación {Id}. Mensajes: {Total}, Para resumir: {ParaResumir}",
            conversacion.IdConversacion, historial.Count, mensajesParaResumir);

        try
        {
            var systemPrompt = "Genera un resumen breve y directo. NO uses razonamiento interno. NO uses etiquetas <thinking>. Responde SOLO con el texto del resumen en español.";
            var userMessage = $@"Resume esta conversación en 3-5 líneas. Sé conciso y directo. Responde solo en español.

CONVERSACIÓN:
{contenidoHistorial}

RESUMEN (3-5 líneas, sin pensar en voz alta):";

            var mensajesHistorial = new List<Mensaje>
            {
                new()
                {
                    Rol = RolMensaje.User,
                    Contenido = userMessage,
                    FechaHora = DateTime.UtcNow
                }
            };

            var resumen = await ollamaService.SendMessageAsync(
                mensajesHistorial, null, systemPrompt, null, 1024, cancellationToken);

            _logger.LogInformation("Respuesta cruda de Ollama para resumen: {Respuesta}",
                resumen?.Length > 200 ? resumen[..200] + "..." : resumen);

            resumen = LimpiarRespuesta(resumen);

            if (string.IsNullOrWhiteSpace(resumen))
            {
                _logger.LogWarning("La IA devolvió un resumen vacío para conversación {Id}, usando fallback", conversacion.IdConversacion);
                return GenerarResumenFallback(historial, longitudResumen);
            }

            if (resumen.Length > longitudResumen)
                resumen = resumen[..longitudResumen];

            _logger.LogInformation("Resumen generado para conversación {Id}: {Longitud} caracteres",
                conversacion.IdConversacion, resumen.Length);

            return resumen;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar resumen para conversación {IdConversacion}", conversacion.IdConversacion);
        }

        return GenerarResumenFallback(historial, longitudResumen);
    }

    private string GenerarResumenFallback(List<Mensaje> historial, int longitudResumen)
    {
        var mensajesUsuario = historial.Where(m => m.Rol == RolMensaje.User).ToList();
        var totalMensajes = historial.Count;
        var usuarioCount = mensajesUsuario.Count;
        var asistenteCount = historial.Count(m => m.Rol == RolMensaje.Assistant);

        var stopwords = new HashSet<string>
        {
            "el","la","los","las","un","una","de","del","al","en","por","para","con","sin","sobre",
            "que","qué","cómo","cuál","dónde","yo","tú","el","ella","me","te","se","nos","les",
            "si","no","y","o","pero","como","lo","le","mi","tu","su","esto","esta","ese","esa",
            "aquel","aquella","todo","toda","nada","algo","hay","ser","estar","haber","tener",
            "puedo","puede","puedes","hacer","sobre","entre","desde","hasta","más","muy","tan",
            "buen","buena","bueno","buenas","gracias","por","favor","hola","dime","cual",
            "ahora","si","también","aquí","así","bien","mal","uno","dos","tres","cuatro","cinco"
        };

        var frecuencia = new Dictionary<string, int>();
        foreach (var m in mensajesUsuario)
        {
            var palabras = m.Contenido.Split(
                new[] { ' ', ',', '.', ';', ':', '!', '¿', '?', '¡', '(', ')', '"', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var palabra in palabras)
            {
                var limpia = palabra.ToLowerInvariant().Trim();
                if (limpia.Length > 3 && !stopwords.Contains(limpia))
                {
                    frecuencia[limpia] = frecuencia.GetValueOrDefault(limpia) + 1;
                }
            }
        }

        var temasPrincipales = frecuencia
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => kv.Key)
            .ToList();

        var frasesClave = mensajesUsuario
            .Where(m => m.Contenido.Length > 10)
            .Select(m =>
            {
                var txt = m.Contenido.Length > 60 ? m.Contenido[..60] + "..." : m.Contenido;
                return $"\"{txt}\"";
            })
            .TakeLast(4)
            .ToList();

        var primeraConsulta = mensajesUsuario.Count > 0
            ? mensajesUsuario.First().Contenido
            : "";
        if (primeraConsulta.Length > 50) primeraConsulta = primeraConsulta[..50] + "...";

        var ultimaConsulta = mensajesUsuario.Count > 0
            ? mensajesUsuario.Last().Contenido
            : "";
        if (ultimaConsulta.Length > 50) ultimaConsulta = ultimaConsulta[..50] + "...";

        var partes = new List<string>();

        partes.Add($"Conversación de {totalMensajes} mensajes ({usuarioCount} del usuario, {asistenteCount} del asistente).");

        if (temasPrincipales.Count > 0)
        {
            partes.Add($"Temas principales: {string.Join(", ", temasPrincipales)}.");
        }

        if (mensajesUsuario.Count > 0)
        {
            partes.Add($"Primera consulta: {primeraConsulta}");
        }

        if (ultimaConsulta != primeraConsulta && mensajesUsuario.Count > 1)
        {
            partes.Add($"Consulta más reciente: {ultimaConsulta}");
        }

        if (frasesClave.Count > 0)
        {
            partes.Add($"Preguntas destacadas: {string.Join("; ", frasesClave)}.");
        }

        var resumen = string.Join(" ", partes);

        if (resumen.Length > longitudResumen)
            resumen = resumen[..longitudResumen];

        _logger.LogInformation("Resumen fallback generado: {Longitud} caracteres, {Temas} temas extraídos",
            resumen.Length, temasPrincipales.Count);

        return resumen;
    }

    public virtual async Task<bool> RequiereResumenAsync(Conversacion conversacion, int mensajesEnMemoria = 0)
    {
        var config = await _configRepo.GetActivaAsync();
        var maxMensajes = config?.MaximoMensajesContexto ?? 20;

        var totalMensajesDb = await _mensajeRepository.GetByConversacionIdAsync(conversacion.IdConversacion);
        var total = totalMensajesDb.Count() + mensajesEnMemoria;

        _logger.LogInformation("Verificando resumen: {Total} mensajes en BD + {EnMemoria} en memoria = {TotalFinal}. Umbral: {Umbral}",
            totalMensajesDb.Count(), mensajesEnMemoria, total, (int)(maxMensajes * 1.5));

        return total > maxMensajes * 1.5;
    }

    private static string LimpiarRespuesta(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var thinkingStart = texto.IndexOf("<thinking>", StringComparison.OrdinalIgnoreCase);
        if (thinkingStart >= 0)
        {
            var thinkingEnd = texto.IndexOf("</thinking>", thinkingStart, StringComparison.OrdinalIgnoreCase);
            if (thinkingEnd >= 0)
                texto = texto[(thinkingEnd + 11)..].Trim();
        }

        return texto.Trim().Trim('"', '\'', '.', '¡', '!', '¿', '?').Trim();
    }

    public Task<string> GenerarTituloAsync(string primerMensaje, string respuesta, IOllamaService ollamaService, CancellationToken cancellationToken = default)
    {
        var promptTitulo = $@"Genera un título descriptivo y corto (máximo 8 palabras) para una conversación basada en el siguiente intercambio.

PRIMER MENSAJE DEL USUARIO: {primerMensaje}

RESPUESTA DEL ASISTENTE: {respuesta}

TÍTULO:";

        var mensajes = new List<Mensaje>
        {
            new()
            {
                Rol = RolMensaje.User,
                Contenido = promptTitulo,
                FechaHora = DateTime.UtcNow
            }
        };

        return ollamaService.SendMessageAsync(mensajes, null, null, null, null, cancellationToken);
    }

    private static int CalcularTokensEstimados(List<Mensaje> mensajes, string? resumen)
    {
        var total = 0;
        foreach (var m in mensajes)
        {
            total += m.Contenido.Length / 4;
        }
        if (!string.IsNullOrWhiteSpace(resumen))
        {
            total += resumen.Length / 4;
        }
        return total;
    }
}

public class ContextoConstruido
{
    public List<Mensaje> MensajesRecientes { get; set; } = new();
    public string? ResumenContexto { get; set; }
    public int CantidadMensajesEnviados { get; set; }
    public int CantidadTokensEstimados { get; set; }
    public long TiempoConstruccionMs { get; set; }
}
