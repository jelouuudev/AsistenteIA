using Asistente.Domain.Enums;

namespace Asistente.Domain.Entities;

public class Mensaje
{
    public int IdMensaje { get; set; }
    public int IdConversacion { get; set; }
    public RolMensaje Rol { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public long? TiempoRespuestaMs { get; set; }

    public Conversacion Conversacion { get; set; } = null!;
}
