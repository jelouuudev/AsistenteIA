namespace Asistente.Application.DTOs;

/// <summary>
/// DTO para respuesta de chat
/// </summary>
public class ChatResponseDto
{
    /// <summary>
    /// Identificador de la conversación
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Respuesta generada por la IA
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Tiempo de respuesta en milisegundos
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Indica si la operación fue exitosa
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensaje de error si la operación falló
    /// </summary>
    public string? Error { get; set; }
}
