using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities.Aprobaciones;

/// <summary>
/// Solicitud de aprobación (ETAPA 19, punto 5). Representa una acción sensible que requiere
/// intervención humana antes de ejecutarse. El plan queda pausado hasta su resolución.
/// </summary>
public class ApprovalRequest
{
    public int IdApproval { get; set; }

    /// <summary>Código legible, p.ej. APROV-00042.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Plan al que pertenece la acción que requiere aprobación.</summary>
    public int IdPlan { get; set; }

    /// <summary>Tipo de aprobación (punto 7).</summary>
    public TipoAprobacion Tipo { get; set; } = TipoAprobacion.Operacional;

    /// <summary>Estado del ciclo de vida (punto 6).</summary>
    public EstadoAprobacion Estado { get; set; } = EstadoAprobacion.Pendiente;

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    /// <summary>Vencimiento calculado a partir de la política. Null = sin vencimiento.</summary>
    public DateTime? FechaVencimiento { get; set; }

    /// <summary>Usuario que disparó la acción que requiere aprobación.</summary>
    public int Solicitante { get; set; }

    /// <summary>Motivo / contexto de la solicitud.</summary>
    public string? Observaciones { get; set; }

    /// <summary>Id de la política aplicada (null = política por defecto).</summary>
    public int? IdPolicy { get; set; }

    // Navegación
    public ApprovalPolicy? Policy { get; set; }
    public ICollection<ApprovalAssignee> Asignados { get; set; } = new List<ApprovalAssignee>();
    public ICollection<ApprovalDecision> Decisiones { get; set; } = new List<ApprovalDecision>();
}
