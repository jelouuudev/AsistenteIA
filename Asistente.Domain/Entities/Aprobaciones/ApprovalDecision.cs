using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities.Aprobaciones;

/// <summary>
/// Decisión individual tomada por un aprobador sobre una solicitud (ETAPA 19, punto 6 actividad 6).
/// Cada comentario se conserva en la auditoría.
/// </summary>
public class ApprovalDecision
{
    public int IdDecision { get; set; }
    public int IdApproval { get; set; }
    public int IdUsuario { get; set; }

    /// <summary>Aprobar / Rechazar / Delegar.</summary>
    public string Decision { get; set; } = "Aprobar"; // Aprobar, Rechazar, Delegar

    /// <summary>Motivo, observaciones o recomendaciones (se conservan en auditoría).</summary>
    public string? Comentario { get; set; }

    public DateTime FechaDecision { get; set; } = DateTime.UtcNow;

    // Navegación
    public ApprovalRequest? Approval { get; set; }
}
