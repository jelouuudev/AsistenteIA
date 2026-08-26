namespace Asistente.Domain.Entities;

/// <summary>
/// Configuración global del Motor de Workflows. Se administra desde la aplicación
/// (Requerimiento Funcional - Actividad 10). Solo existe un registro (Id = 1).
/// </summary>
public class ConfiguracionWorkflow
{
    public int IdConfiguracion { get; set; } = 1;
    public int ReintentosMaximos { get; set; } = 2;
    public int TiempoMaximoPasoMs { get; set; } = 30000;
    public int TiempoMaximoFlujoMs { get; set; } = 180000;
    public bool ConfirmacionesObligatorias { get; set; } = true;
    public int LimitePasosPorWorkflow { get; set; } = 10;
    public DateTime FechaActualizacion { get; set; }
}
