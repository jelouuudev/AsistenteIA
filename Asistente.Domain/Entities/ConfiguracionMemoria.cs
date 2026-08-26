namespace Asistente.Domain.Entities;

public class ConfiguracionMemoria
{
    public int IdConfiguracion { get; set; }
    public int MaximoMensajesContexto { get; set; } = 20;
    public int MaximoTokensContexto { get; set; } = 4096;
    public int LongitudResumen { get; set; } = 500;
    public int CantidadConversacionesVisibles { get; set; } = 50;
    public bool Activo { get; set; } = true;
}
