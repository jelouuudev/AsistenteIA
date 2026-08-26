namespace Asistente.Domain.Entities;

/// <summary>
/// Configuración global del Motor de Eventos Empresariales (ETAPA 13 - Actividades 6/10).
/// Solo existe un registro (Id = 1). Administra reintentos, tiempos máximos y concurrencia.
/// </summary>
public class ConfiguracionEventoMotor
{
    public int IdConfiguracion { get; set; } = 1;

    /// <summary>Número máximo de reintentos por evento fallido (Actividad 6).</summary>
    public int ReintentosMaximos { get; set; } = 3;

    /// <summary>Intervalo base entre reintentos (ms). Se aplica backoff progresivo.</summary>
    public int IntervaloReintentoMs { get; set; } = 5000;

    /// <summary>Tiempo máximo de procesamiento de un evento (ms).</summary>
    public int TiempoMaximoEventoMs { get; set; } = 120000;

    /// <summary>Cantidad máxima de eventos procesados en paralelo (Actividad 10).</summary>
    public int EventosSimultaneosMax { get; set; } = 5;

    /// <summary>Frecuencia de sondeo del procesador de eventos en cola (ms, Actividad 10).</summary>
    public int FrecuenciaProcesadorMs { get; set; } = 3000;

    public DateTime FechaActualizacion { get; set; }
}
