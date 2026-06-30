using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class ChatService : IChatService
{
    private readonly IConversacionRepository _conversacionRepository;
    private readonly IMensajeRepository _mensajeRepository;
    private readonly IOllamaService _ollamaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IConversacionRepository conversacionRepository,
        IMensajeRepository mensajeRepository,
        IOllamaService ollamaService,
        IUnitOfWork unitOfWork,
        ILogger<ChatService> logger)
    {
        _conversacionRepository = conversacionRepository;
        _mensajeRepository = mensajeRepository;
        _ollamaService = ollamaService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<MensajeResponse> ProcesarMensajeAsync(MensajeRequest request, CancellationToken cancellationToken = default)
    {
        var response = new MensajeResponse();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Mensaje))
            {
                response.Exitoso = false;
                response.Error = "El mensaje no puede estar vacío.";
                return response;
            }

            var conversacion = await ObtenerOcrearConversacionAsync(request.IdConversacion);

            var mensajeUsuario = new Mensaje
            {
                IdConversacion = conversacion.IdConversacion,
                Rol = RolMensaje.User,
                Contenido = request.Mensaje,
                FechaHora = DateTime.UtcNow
            };

            await _mensajeRepository.CreateAsync(mensajeUsuario);

            var historial = BuildHistorial(conversacion, mensajeUsuario);

            var inicio = DateTime.UtcNow;
            var respuestaIa = await _ollamaService.SendMessageAsync(historial, cancellationToken);
            var tiempoMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;

            var mensajeIa = new Mensaje
            {
                IdConversacion = conversacion.IdConversacion,
                Rol = RolMensaje.Assistant,
                Contenido = respuestaIa,
                FechaHora = DateTime.UtcNow,
                TiempoRespuestaMs = tiempoMs
            };

            await _mensajeRepository.CreateAsync(mensajeIa);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            response.IdConversacion = conversacion.IdConversacion;
            response.Respuesta = respuestaIa;
            response.TiempoRespuestaMs = tiempoMs;
            response.Exitoso = true;
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout al procesar mensaje.");
            response.Exitoso = false;
            response.Error = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error de operación al procesar mensaje.");
            response.Exitoso = false;
            response.Error = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al procesar mensaje.");
            response.Exitoso = false;
            response.Error = "Ocurrió un error inesperado. Intente nuevamente.";
        }

        return response;
    }

    private async Task<Conversacion> ObtenerOcrearConversacionAsync(int? idConversacion)
    {
        if (idConversacion.HasValue)
        {
            var conversacion = await _conversacionRepository.GetByIdAsync(idConversacion.Value);
            if (conversacion != null)
            {
                return conversacion;
            }
        }

        var nueva = new Conversacion
        {
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoConversacion.Activa
        };

        await _conversacionRepository.CreateAsync(nueva);
        await _unitOfWork.SaveChangesAsync();

        return nueva;
    }

    private static List<Mensaje> BuildHistorial(Conversacion conversacion, Mensaje mensajeActual)
    {
        var historial = new List<Mensaje>();

        if (conversacion.Mensajes is { Count: > 0 })
        {
            historial.AddRange(conversacion.Mensajes.OrderBy(m => m.FechaHora));
        }

        historial.Add(mensajeActual);

        return historial;
    }
}
