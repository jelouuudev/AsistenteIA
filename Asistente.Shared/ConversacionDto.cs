namespace Asistente.Shared;

public class ConversacionDto
{
    public int IdConversacion { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Titulo { get; set; }
    public int UsuarioPropietario { get; set; }
    public DateTime? FechaUltimaActividad { get; set; }
    public string? ResumenContexto { get; set; }
    public int TotalMensajes { get; set; }
    public int? IdAsistente { get; set; }
    public List<MensajeDto> Mensajes { get; set; } = new();
}

public class MensajeDto
{
    public int IdMensaje { get; set; }
    public int IdConversacion { get; set; }
    public string Rol { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public long? TiempoRespuestaMs { get; set; }
}

public class ConfiguracionMemoriaDto
{
    public int IdConfiguracion { get; set; }
    public int MaximoMensajesContexto { get; set; }
    public int MaximoTokensContexto { get; set; }
    public int LongitudResumen { get; set; }
    public int CantidadConversacionesVisibles { get; set; }
    public bool Activo { get; set; }
}

public class ActualizarConfiguracionMemoriaRequest
{
    public int MaximoMensajesContexto { get; set; } = 20;
    public int MaximoTokensContexto { get; set; } = 4096;
    public int LongitudResumen { get; set; } = 500;
    public int CantidadConversacionesVisibles { get; set; } = 50;
    public bool Activo { get; set; } = true;
}

public class RenombrarConversacionRequest
{
    public string Titulo { get; set; } = string.Empty;
}

public class DebugContextoDto
{
    public int IdConversacion { get; set; }
    public string PromptFinal { get; set; } = string.Empty;
    public int CantidadMensajesEnviados { get; set; }
    public int CantidadTokensEstimados { get; set; }
    public string? ResumenUtilizado { get; set; }
    public long TiempoConstruccionMs { get; set; }
    public DateTime FechaGeneracion { get; set; }
}

public class ConversacionListDto
{
    public int IdConversacion { get; set; }
    public string? Titulo { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaUltimaActividad { get; set; }
    public int TotalMensajes { get; set; }
    public int? IdAsistente { get; set; }
    public string? Fecha { get; set; }
}
