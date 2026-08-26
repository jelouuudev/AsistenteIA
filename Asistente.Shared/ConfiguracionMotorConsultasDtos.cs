namespace Asistente.Shared;

public class ConfiguracionMotorConsultasDto
{
    public int IdConfiguracion { get; set; }
    public int TiempoMaximoEjecucionSegundos { get; set; }
    public int MaximoRegistros { get; set; }
    public int MaxConsultasSimultaneas { get; set; }
    public int? IdConexionPredeterminada { get; set; }
    public string? NombreConexionPredeterminada { get; set; }
    public bool Activo { get; set; }
}

public class ActualizarConfiguracionMotorConsultasRequest
{
    public int TiempoMaximoEjecucionSegundos { get; set; } = 15;
    public int MaximoRegistros { get; set; } = 100;
    public int MaxConsultasSimultaneas { get; set; } = 5;
    public int? IdConexionPredeterminada { get; set; }
    public bool Activo { get; set; } = true;
}

public class ProcesarPreguntaRequest
{
    public string Pregunta { get; set; } = string.Empty;
}

public class ResultadoProcesarPreguntaDto
{
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
    public string Respuesta { get; set; } = string.Empty;
    public string? Fuente { get; set; }
    public int? IdConexion { get; set; }
    public string? ConsultaSql { get; set; }
    public int? CantidadRegistros { get; set; }
    public string? Tipo { get; set; }
    public List<Dictionary<string, object?>>? Datos { get; set; }
}

public class DecisionMotorDto
{
    public string Tipo { get; set; } = string.Empty;
    public bool UsarRag { get; set; }
    public bool UsarSql { get; set; }
    public string Justificacion { get; set; } = string.Empty;
}
