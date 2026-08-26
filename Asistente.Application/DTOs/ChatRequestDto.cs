namespace Asistente.Application.DTOs;

/// <summary>
/// DTO para solicitud de chat
/// </summary>
public class ChatRequestDto
{
    /// <summary>
    /// Identificador de la conversación (opcional para nuevas conversaciones)
    /// </summary>
    public int? ConversationId { get; set; }

    /// <summary>
    /// Mensaje del usuario
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
