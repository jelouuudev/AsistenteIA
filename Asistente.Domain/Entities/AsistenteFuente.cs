namespace Asistente.Domain.Entities;

public class AsistenteFuente
{
    public int IdAsistente { get; set; }
    public int IdFuente { get; set; }
    public bool Activo { get; set; } = true;
    public int Prioridad { get; set; } = 5;

    public Asistente? Asistente { get; set; }
    public FuenteConocimiento? Fuente { get; set; }
}
