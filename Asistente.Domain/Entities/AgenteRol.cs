namespace Asistente.Domain.Entities;

/// <summary>
/// Relación muchos-a-muchos entre un Agente (Asistente) y los Roles que pueden usarlo.
/// ETAPA 16 - Actividad 6 / Regla 1.
/// </summary>
public class AgenteRol
{
    public int IdAgenteRol { get; set; }
    public int IdAsistente { get; set; }
    public int IdRol { get; set; }
    public bool Activo { get; set; } = true;

    public Asistente? Asistente { get; set; }
    public Rol? Rol { get; set; }
}
