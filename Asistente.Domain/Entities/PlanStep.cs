namespace Asistente.Domain.Entities;

/// <summary>
/// Paso individual de un plan. Cada paso tiene un tipo (Agent, Tool, Workflow, RAG, Validation, Approval)
/// y referencia al agente/herramienta/workflow concreto que lo ejecuta.
/// </summary>
public class PlanStep
{
    public int IdStep { get; set; }
    public int IdPlan { get; set; }
    public int Orden { get; set; }
    public string Tipo { get; set; } = "Agent"; // Agent, Tool, Workflow, RAG, Validation, Approval
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente, EnEjecucion, Completado, Error, Omitido
    public string? Resultado { get; set; }
    public int? IdAsistente { get; set; }   // para tipo Agent
    public string? CodigoHerramienta { get; set; } // para tipo Tool
    public int? IdWorkflow { get; set; }     // para tipo Workflow
    public int Intentos { get; set; } // reintentos realizados
    /// <summary>
    /// Entrada específica del paso (sub-consulta autocontenida asignada por el
    /// PlanBuilder para ramas paralelas). Si es null, el paso usa el objetivo
    /// del plan. Contrato máquina-máquina, no se muestra en UI.
    /// </summary>
    public string? Entrada { get; set; }

    public Plan? Plan { get; set; }
}
