using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Representa una herramienta reutilizable del Motor de Herramientas (Tool Orchestrator).
/// Las herramientas están desacopladas del modelo de IA y se ejecutan bajo controles de autorización.
/// </summary>
public class Herramienta
{
    public int IdHerramienta { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Utilidad";
    public bool Activa { get; set; } = true;
    public bool RequierePermiso { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<AsistenteHerramienta> AsistentesHerramientas { get; set; } = new List<AsistenteHerramienta>();
}
