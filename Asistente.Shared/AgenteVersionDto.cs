using Asistente.Domain.Entities;

namespace Asistente.Shared;

public class AgenteVersionDto
{
    public int IdAgenteVersion { get; set; }
    public int IdAsistente { get; set; }
    public int Version { get; set; }
    public string? ModeloIA { get; set; }
    public EstadoAgente Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }
}
