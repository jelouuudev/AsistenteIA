using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Métricas de IA por interacción (ETAPA 14 - Actividad 15).
/// </summary>
public class MetricasIA
{
    public int IdMetrica { get; set; }
    public int IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    public long TiempoGeneracionMs { get; set; }
    public long TiempoRecuperacionRagMs { get; set; }
    public long TiempoConsultaSqlMs { get; set; }
    public long TiempoHerramientasMs { get; set; }
    public int DocumentosRecuperados { get; set; }
    public int HerramientasEjecutadas { get; set; }
    public int? TokensEntrada { get; set; }
    public int? TokensSalida { get; set; }
}
