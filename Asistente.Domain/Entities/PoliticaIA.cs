using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Política de IA configurable (ETAPA 14 - Actividades 11 y 12).
/// Tipo indica la dimensión (ModeloPermitido, MaxContexto, MaxResultadosRag,
/// HerramientasPermitidas, TiempoMaxRespuesta, MaxEjecuciones, FuentesAutorizadas).
/// </summary>
public class PoliticaIA
{
    public int IdPolitica { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
