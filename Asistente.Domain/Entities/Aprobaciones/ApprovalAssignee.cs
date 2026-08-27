namespace Asistente.Domain.Entities.Aprobaciones;

/// <summary>
/// Aprobador asignado a una solicitud (ETAPA 19, actividades 4 y 5). Soporta aprobación simple,
/// múltiple, unanimidad y mayoría. EsPrincipal marca al aprobador primario (p.ej. supervisor).
/// </summary>
public class ApprovalAssignee
{
    public int IdAssignee { get; set; }
    public int IdApproval { get; set; }
    public int IdUsuario { get; set; }

    /// <summary>True si es el aprobador principal (Supervisor Comercial, Gerente, etc.).</summary>
    public bool EsPrincipal { get; set; }

    /// <summary>Estado del asignado: Pendiente, Aprobado, Rechazado, Delegado.</summary>
    public string Estado { get; set; } = "Pendiente";

    // Navegación
    public ApprovalRequest? Approval { get; set; }
}
