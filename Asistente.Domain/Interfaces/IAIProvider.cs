namespace Asistente.Domain.Interfaces;

/// <summary>
/// Interfaz para proveedores de Inteligencia Artificial
/// Permite desacoplar la implementación del proveedor de IA
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Envía un mensaje al modelo de IA y obtiene una respuesta
    /// </summary>
    /// <param name="messages">Historial de mensajes de la conversación</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Respuesta generada por el modelo de IA</returns>
    Task<string> SendMessageAsync(IEnumerable<string> messages, CancellationToken cancellationToken = default);
}
