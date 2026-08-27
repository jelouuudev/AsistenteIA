namespace Asistente.Domain.Entities.Aprobaciones;

/// <summary>
/// Política de aprobación configurable (ETAPA 19, punto 5 / actividad 2).
/// Define reglas de mínimo de aprobaciones, unanimidad, delegación y vencimiento.
/// </summary>
public class ApprovalPolicy
{
    public int IdPolicy { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Cantidad mínima de aprobaciones requeridas (mayoría).</summary>
    public int CantidadMinimaAprobaciones { get; set; } = 1;

    /// <summary>Si true, TODOS los asignados deben aprobar.</summary>
    public bool RequiereUnanimidad { get; set; }

    /// <summary>Permite que un aprobador delegue su decisión a otro.</summary>
    public bool PermiteDelegacion { get; set; } = true;

    /// <summary>Tiempo máximo en horas antes de expirar. 0 = sin vencimiento.</summary>
    public int TiempoMaximoHoras { get; set; } = 0;

    public bool Activo { get; set; } = true;
}
