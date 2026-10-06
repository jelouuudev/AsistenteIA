using System.Collections.Generic;

namespace Asistente.Shared;

/// <summary>DTOs del API Gateway Empresarial y Conectores (ETAPA 20). Viven en
/// Shared para que el API, Application y Web los compartan. Los secretos jamás
/// viajan en lectura: solo tipo y existencia.</summary>
public class CrearConectorRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "REST";
    public string? Descripcion { get; set; }
    public bool RequierePermiso { get; set; } = true;
    public Dictionary<string, string> Configuracion { get; set; } = new();
    public CrearCredencialRequest? Credencial { get; set; }
    public CrearPoliticaRequest? Politica { get; set; }
}

public class CrearCredencialRequest
{
    public string Tipo { get; set; } = "None";
    public string? NombreUsuario { get; set; }
    public string? Secreto { get; set; }
    public Dictionary<string, string> Parametros { get; set; } = new();
}

public class CrearPoliticaRequest
{
    public int TimeoutSegundos { get; set; } = 30;
    public int MaxReintentos { get; set; } = 2;
    public int IntervaloReintentoMs { get; set; } = 1000;
    public int RateLimitPorMinuto { get; set; } = 60;
    public int CircuitBreakerUmbralFallos { get; set; } = 5;
    public int CircuitBreakerSegundosAbierto { get; set; } = 60;
}

public class ConectorDto
{
    public int IdConnector { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public bool RequierePermiso { get; set; }
    public Dictionary<string, string> Configuracion { get; set; } = new();
    public string? TipoCredencial { get; set; }
    public bool TieneCredencial { get; set; }
    public CrearPoliticaRequest? Politica { get; set; }
}

public class EjecutarConectorRequest
{
    public string? Operacion { get; set; }
    public string? Recurso { get; set; }
    public string? Metodo { get; set; }
    public string? Cuerpo { get; set; }
    public string? TipoContenido { get; set; }
    public Dictionary<string, string>? Cabeceras { get; set; }
    public Dictionary<string, string>? Parametros { get; set; }
}

/// <summary>Métricas agregadas por conector para el Dashboard.</summary>
public class ConnectorMetricas
{
    public int IdConnector { get; set; }
    public long Total { get; set; }
    public long Exitosas { get; set; }
    public long Fallidas { get; set; }
    public double LatenciaPromedioMs { get; set; }
    public double LatenciaMaxMs { get; set; }
    public long ReintentosTotales { get; set; }
}

/// <summary>Fila de auditoría de conector sin secretos (para API y Web).</summary>
public class ConectorEjecucionDto
{
    public int IdEjecucion { get; set; }
    public int IdConnector { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public string? Destino { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int? CodigoRespuesta { get; set; }
    public long LatenciaMs { get; set; }
    public int Reintentos { get; set; }
    public string? Error { get; set; }
    public string? RespuestaResumen { get; set; }
    public System.DateTime Fecha { get; set; }
}
