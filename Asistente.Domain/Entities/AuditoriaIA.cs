using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Registro de auditoría de IA para reconstruir una interacción (ETAPA 14 - Actividad 9).
/// NO almacena datos sensibles: el contenido de la pregunta/respuesta se enmascara en origen.
/// </summary>
public class AuditoriaIA
{
    public int IdAuditoriaIA { get; set; }
    public int IdUsuario { get; set; }
    public int? IdConversacion { get; set; }
    public int? IdAsistente { get; set; }
    public int? VersionAgente { get; set; }
    public string? AsistenteNombre { get; set; }
    public string Modelo { get; set; } = string.Empty;
    public string Pregunta { get; set; } = string.Empty;      // enmascarada si contiene sensible
    public string? PromptUtilizado { get; set; }               // solo el sistema, no contenido RAG
    public string? HerramientasUtilizadas { get; set; }       // JSON de nombres
    public string? FuentesConsultadas { get; set; }           // JSON de nombres de fuentes
    public string Respuesta { get; set; } = string.Empty;      // enmascarada si contiene sensible
    public long TiempoRespuestaMs { get; set; }
    public string Resultado { get; set; } = "Exitoso";         // Exitoso | Error | Bloqueado
    public string? Detalle { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
}
