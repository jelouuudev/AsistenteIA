namespace Asistente.Application.DTOs;

/// <summary>
/// DTO para conversación
/// </summary>
public class ConversationDto
{
    /// <summary>
    /// Identificador de la conversación
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Fecha de inicio
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Fecha de fin (opcional)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Estado de la conversación
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Mensajes de la conversación
    /// </summary>
    public List<MessageDto> Messages { get; set; } = new();
}
