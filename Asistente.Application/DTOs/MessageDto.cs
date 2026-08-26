namespace Asistente.Application.DTOs;

/// <summary>
/// DTO para mensaje
/// </summary>
public class MessageDto
{
    /// <summary>
    /// Identificador del mensaje
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Identificador de la conversación
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Rol del mensaje (user, assistant)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Contenido del mensaje
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora del mensaje
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Tiempo de respuesta en milisegundos (solo para mensajes del asistente)
    /// </summary>
    public long? ResponseTimeMs { get; set; }
}
