using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class MemoriaService : IMemoriaService
{
    private readonly IConversacionRepository _conversacionRepository;
    private readonly IMensajeRepository _mensajeRepository;
    private readonly IConfiguracionMemoriaRepository _configuracionMemoriaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<MemoriaService> _logger;

    public MemoriaService(
        IConversacionRepository conversacionRepository,
        IMensajeRepository mensajeRepository,
        IConfiguracionMemoriaRepository configuracionMemoriaRepository,
        IUnitOfWork unitOfWork,
        IOllamaService ollamaService,
        ILogger<MemoriaService> logger)
    {
        _conversacionRepository = conversacionRepository;
        _mensajeRepository = mensajeRepository;
        _configuracionMemoriaRepository = configuracionMemoriaRepository;
        _unitOfWork = unitOfWork;
        _ollamaService = ollamaService;
        _logger = logger;
    }

    private async Task<int> ObtenerMaximoConversacionesVisiblesAsync()
    {
        var config = await _configuracionMemoriaRepository.GetActivaAsync();
        return config?.CantidadConversacionesVisibles ?? 50;
    }

    public async Task<IEnumerable<ConversacionListDto>> ObtenerConversacionesAsync(int usuarioId)
    {
        var maximoVisibles = await ObtenerMaximoConversacionesVisiblesAsync();
        var conversaciones = (await _conversacionRepository.GetByUsuarioAsync(usuarioId))
            .Take(maximoVisibles);
        var resultado = new List<ConversacionListDto>();
        foreach (var c in conversaciones)
        {
            var dto = MapToListDto(c);
            dto.TotalMensajes = await _mensajeRepository.CountByConversacionIdAsync(c.IdConversacion);
            resultado.Add(dto);
        }
        return resultado;
    }

    public async Task<IEnumerable<ConversacionListDto>> BuscarConversacionesAsync(int usuarioId, string titulo)
    {
        var maximoVisibles = await ObtenerMaximoConversacionesVisiblesAsync();
        var conversaciones = (await _conversacionRepository.SearchByTituloAsync(usuarioId, titulo))
            .Take(maximoVisibles);
        var resultado = new List<ConversacionListDto>();
        foreach (var c in conversaciones)
        {
            var dto = MapToListDto(c);
            dto.TotalMensajes = await _mensajeRepository.CountByConversacionIdAsync(c.IdConversacion);
            resultado.Add(dto);
        }
        return resultado;
    }

    public async Task<ConversacionDto?> ObtenerConversacionAsync(int id)
    {
        var conversacion = await _conversacionRepository.GetByIdAsync(id);
        if (conversacion == null) return null;

        return new ConversacionDto
        {
            IdConversacion = conversacion.IdConversacion,
            FechaInicio = conversacion.FechaInicio,
            FechaFin = conversacion.FechaFin,
            Estado = conversacion.Estado.ToString(),
            Titulo = conversacion.Titulo,
            UsuarioPropietario = conversacion.UsuarioPropietario,
            FechaUltimaActividad = conversacion.FechaUltimaActividad,
            ResumenContexto = conversacion.ResumenContexto,
            TotalMensajes = conversacion.TotalMensajes,
            IdAsistente = conversacion.IdAsistente,
            Mensajes = conversacion.Mensajes.Select(m => new MensajeDto
            {
                IdMensaje = m.IdMensaje,
                IdConversacion = m.IdConversacion,
                Rol = m.Rol.ToString(),
                Contenido = m.Contenido,
                FechaHora = m.FechaHora,
                TiempoRespuestaMs = m.TiempoRespuestaMs
            }).ToList()
        };
    }

    public async Task<ConversacionDto> CrearConversacionAsync(int usuarioId)
    {
        var conversacion = new Conversacion
        {
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoConversacion.Activa,
            UsuarioPropietario = usuarioId,
            FechaUltimaActividad = DateTime.UtcNow,
            TotalMensajes = 0
        };

        await _conversacionRepository.CreateAsync(conversacion);
        await _unitOfWork.SaveChangesAsync();

        return new ConversacionDto
        {
            IdConversacion = conversacion.IdConversacion,
            FechaInicio = conversacion.FechaInicio,
            Estado = conversacion.Estado.ToString(),
            UsuarioPropietario = conversacion.UsuarioPropietario,
            FechaUltimaActividad = conversacion.FechaUltimaActividad,
            TotalMensajes = conversacion.TotalMensajes
        };
    }

    public async Task RenombrarConversacionAsync(int id, string titulo)
    {
        var conversacion = await _conversacionRepository.GetByIdAsync(id);
        if (conversacion == null)
            throw new InvalidOperationException($"La conversación {id} no existe.");

        conversacion.Titulo = titulo;
        await _conversacionRepository.UpdateAsync(conversacion);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ArchivarConversacionAsync(int id)
    {
        var conversacion = await _conversacionRepository.GetByIdAsync(id);
        if (conversacion == null)
            throw new InvalidOperationException($"La conversación {id} no existe.");

        conversacion.Estado = EstadoConversacion.Archivada;
        await _conversacionRepository.UpdateAsync(conversacion);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task EliminarConversacionAsync(int id)
    {
        var conversacion = await _conversacionRepository.GetByIdAsync(id);
        if (conversacion == null)
            throw new InvalidOperationException($"La conversación {id} no existe.");

        conversacion.Estado = EstadoConversacion.Eliminada;
        await _conversacionRepository.UpdateAsync(conversacion);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<string?> GenerarTituloAsync(int idConversacion, string primerMensaje, string respuesta)
    {
        try
        {
            var promptTitulo = $@"Genera un título descriptivo y corto (máximo 8 palabras) para una conversación basada en el siguiente intercambio.

PRIMER MENSAJE: {primerMensaje}

RESPUESTA: {respuesta}

IMPORTANTE: Responde SOLO con el título, sin explicaciones, sin pensamiento, sin etiquetas.";

            var mensajes = new List<Mensaje>
            {
                new()
                {
                    Rol = RolMensaje.User,
                    Contenido = promptTitulo,
                    FechaHora = DateTime.UtcNow
                }
            };

            var titulo = await _ollamaService.SendMessageAsync(mensajes, CancellationToken.None);

            titulo = LimpiarTitulo(titulo);

            if (string.IsNullOrWhiteSpace(titulo))
            {
                _logger.LogWarning("La IA devolvió un título vacío para conversación {Id}", idConversacion);
                return null;
            }

            if (titulo.Length > 200)
                titulo = titulo[..200];

            _logger.LogInformation("Título generado para conversación {Id}: {Titulo}", idConversacion, titulo);
            return titulo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo generar título automático para conversación {Id}", idConversacion);
            return null;
        }
    }

    private static string LimpiarTitulo(string titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            return string.Empty;

        var thinkingStart = titulo.IndexOf("<thinking>", StringComparison.OrdinalIgnoreCase);
        if (thinkingStart >= 0)
        {
            var thinkingEnd = titulo.IndexOf("</thinking>", thinkingStart, StringComparison.OrdinalIgnoreCase);
            if (thinkingEnd >= 0)
            {
                titulo = titulo[(thinkingEnd + 11)..].Trim();
            }
            else
            {
                var afterTag = titulo[(thinkingStart + 10)..].Trim();
                if (!string.IsNullOrWhiteSpace(afterTag))
                    titulo = afterTag;
            }
        }

        titulo = titulo.Trim().Trim('"', '\'', '.', '¡', '!', '¿', '?');
        var saltos = new[] { '\r', '\n', '\t' };
        titulo = titulo.Trim(saltos).Trim();

        return titulo;
    }

    public async Task<DebugContextoDto> ObtenerDebugContextoAsync(int idConversacion)
    {
        var conversacion = await _conversacionRepository.GetByIdAsync(idConversacion);
        if (conversacion == null)
            throw new InvalidOperationException($"La conversación {idConversacion} no existe.");

        var mensajes = (await _mensajeRepository.GetByConversacionIdAsync(idConversacion))
            .OrderBy(m => m.FechaHora)
            .ToList();

        var promptPartes = new List<string>();

        if (!string.IsNullOrWhiteSpace(conversacion.ResumenContexto))
        {
            promptPartes.Add("[RESUMEN DE CONTEXTO ANTERIOR]");
            promptPartes.Add(conversacion.ResumenContexto);
            promptPartes.Add("");
        }

        promptPartes.Add("[HISTORIAL DE LA CONVERSACIÓN]");
        foreach (var m in mensajes)
        {
            var rol = m.Rol == RolMensaje.User ? "Usuario" : "Asistente";
            promptPartes.Add($"{rol}: {m.Contenido}");
        }

        var promptFinal = string.Join("\n", promptPartes);
        var tokensEstimados = promptFinal.Length / 4;

        return new DebugContextoDto
        {
            IdConversacion = idConversacion,
            PromptFinal = promptFinal,
            CantidadMensajesEnviados = mensajes.Count,
            CantidadTokensEstimados = tokensEstimados,
            ResumenUtilizado = conversacion.ResumenContexto,
            TiempoConstruccionMs = 0,
            FechaGeneracion = DateTime.UtcNow
        };
    }

    private static ConversacionListDto MapToListDto(Conversacion c)
    {
        return new ConversacionListDto
        {
            IdConversacion = c.IdConversacion,
            Titulo = c.Titulo,
            Estado = c.Estado.ToString(),
            FechaInicio = c.FechaInicio,
            FechaUltimaActividad = c.FechaUltimaActividad,
            TotalMensajes = c.TotalMensajes,
            IdAsistente = c.IdAsistente,
            Fecha = (c.FechaUltimaActividad ?? c.FechaInicio).ToString("dd/MM HH:mm")
        };
    }
}
