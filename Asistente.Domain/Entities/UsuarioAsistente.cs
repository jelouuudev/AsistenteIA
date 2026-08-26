namespace Asistente.Domain.Entities;

/// <summary>
/// Asociación explícita de un Usuario con un Asistente autorizado (ETAPA 14 - Actividad 4).
/// Un usuario solo puede usar los asistentes listados aquí (Regla 2).
/// </summary>
public class UsuarioAsistente
{
    public int IdUsuario { get; set; }
    public int IdAsistente { get; set; }
    public bool Activo { get; set; } = true;

    public Usuario? Usuario { get; set; }
    public Asistente? Asistente { get; set; }
}
