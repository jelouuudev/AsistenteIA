namespace Asistente.Domain.Entities;

/// <summary>
/// Relación muchos-a-muchos entre un Agente (Asistente) y los Workflows autorizados.
/// ETAPA 16 - Actividad 5.
/// </summary>
public class AgenteWorkflow
{
    public int IdAgenteWorkflow { get; set; }
    public int IdAsistente { get; set; }
    public int IdWorkflow { get; set; }
    public bool Activo { get; set; } = true;

    public Asistente? Asistente { get; set; }
    public Workflow? Workflow { get; set; }
}
