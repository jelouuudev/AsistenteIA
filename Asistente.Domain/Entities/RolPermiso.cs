namespace Asistente.Domain.Entities;

/// <summary>
/// Relación Rol -> Permiso (ETAPA 14 - Actividad 3).
/// </summary>
public class RolPermiso
{
    public int IdRol { get; set; }
    public int IdPermiso { get; set; }

    public Rol? Rol { get; set; }
    public Permiso? Permiso { get; set; }
}
