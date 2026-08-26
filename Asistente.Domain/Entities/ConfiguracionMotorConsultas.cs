namespace Asistente.Domain.Entities;

public class ConfiguracionMotorConsultas
{
    public int IdConfiguracion { get; set; }
    public int TiempoMaximoEjecucionSegundos { get; set; } = 15;
    public int MaximoRegistros { get; set; } = 100;
    public int MaxConsultasSimultaneas { get; set; } = 5;
    public int? IdConexionPredeterminada { get; set; }
    public bool Activo { get; set; } = true;
}
