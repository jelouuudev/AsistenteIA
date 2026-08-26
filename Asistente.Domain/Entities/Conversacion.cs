using Asistente.Domain.Enums;

namespace Asistente.Domain.Entities;

public class Conversacion
{
    public int IdConversacion { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public EstadoConversacion Estado { get; set; }
    public string? Titulo { get; set; }
    public int UsuarioPropietario { get; set; }
    public DateTime? FechaUltimaActividad { get; set; }
    public string? ResumenContexto { get; set; }
    public int TotalMensajes { get; set; }
    public int? IdAsistente { get; set; }
    public string? UltimoDocumentoPreferido { get; set; }

    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
}
