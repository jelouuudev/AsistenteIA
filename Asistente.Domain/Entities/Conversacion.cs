using Asistente.Domain.Enums;

namespace Asistente.Domain.Entities;

public class Conversacion
{
    public int IdConversacion { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public EstadoConversacion Estado { get; set; }

    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
}
