using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Configuración global del Motor de Herramientas (Tool Orchestrator).
/// Se administra desde la aplicación (Actividad 10). Solo existe un registro (Id = 1).
/// </summary>
public class ConfiguracionOrchestrator
{
    public int IdConfiguracion { get; set; }
    public bool Habilitado { get; set; } = true;
    public int Prioridad { get; set; } = 100;
    public int TiempoMaximoEjecucionMs { get; set; } = 30000;
    public int MaxEjecucionesSimultaneas { get; set; } = 4;
    public bool RequiereAutorizacion { get; set; } = true;

    // ETAPA 17 - Control de profundidad (Actividad 8)
    public int MaxAgentesPorSolicitud { get; set; } = 5;
    public int MaxProfundidad { get; set; } = 3;
    public int MaxHerramientasPorAgente { get; set; } = 5;
    public int MaxTiempoTotalMs { get; set; } = 120000;
    // Estrategia ante fallo de un nodo: Continuar | Reintentar | Cancelar
    public string EstrategiaError { get; set; } = "Continuar";
    public int ReintentosNodo { get; set; } = 1;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
