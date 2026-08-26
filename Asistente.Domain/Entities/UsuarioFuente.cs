namespace Asistente.Domain.Entities;

/// <summary>
/// Asociación explícita de un Usuario con una Fuente de conocimiento autorizada (ETAPA 14 - Actividad 5).
/// El control de fuentes se aplica independientemente de la solicitud al modelo (Regla 5).
/// </summary>
public class UsuarioFuente
{
    public int IdUsuario { get; set; }
    public int IdFuente { get; set; }
    public bool Activo { get; set; } = true;

    public Usuario? Usuario { get; set; }
    public FuenteConocimiento? Fuente { get; set; }
}
