namespace Asistente.Domain.Entities;

/// <summary>
/// Dependencia dirigida entre dos pasos de un plan (grafo acíclico - DAG).
/// StepOrigen debe completarse antes que StepDestino.
/// </summary>
public class PlanDependency
{
    public int IdDependency { get; set; }
    public int IdPlan { get; set; }
    public int StepOrigen { get; set; }
    public int StepDestino { get; set; }

    public Plan? Plan { get; set; }
}
