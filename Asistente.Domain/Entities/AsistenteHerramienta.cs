namespace Asistente.Domain.Entities;

/// <summary>
/// Relación muchos-a-muchos entre un Asistente y las Herramientas que tiene autorizadas.
/// Permite configurar qué herramientas puede utilizar cada asistente.
/// </summary>
public class AsistenteHerramienta
{
    public int IdAsistente { get; set; }
    public int IdHerramienta { get; set; }
    public bool Activa { get; set; } = true;

    // Navigation properties
    public Asistente? Asistente { get; set; }
    public Herramienta? Herramienta { get; set; }
}
