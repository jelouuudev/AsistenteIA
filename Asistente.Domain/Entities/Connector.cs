using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

/// <summary>
/// Conector externo registrado en el API Gateway Empresarial (ETAPA 20).
/// Los agentes NUNCA consumen APIs externas directamente: toda llamada pasa por
/// el Gateway, que resuelve el conector por su Codigo. El Tipo determina la
/// implementación (REST | SOAP | SMTP | Webhook | Microsoft365 | SharePoint).
/// </summary>
public class Connector
{
    public int IdConnector { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "REST";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Si es true, el Gateway exige el permiso CONNECTOR:{Codigo} al usuario.</summary>
    public bool RequierePermiso { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ConnectorConfiguration> Configuraciones { get; set; } = new List<ConnectorConfiguration>();
    public ICollection<ConnectorCredential> Credenciales { get; set; } = new List<ConnectorCredential>();
    public ICollection<ConnectorPolicy> Politicas { get; set; } = new List<ConnectorPolicy>();
    public ICollection<ConnectorExecution> Ejecuciones { get; set; } = new List<ConnectorExecution>();
}

/// <summary>
/// Configuración no secreta del conector ( URLs base, tenant, rutas, etc.).
/// Los secretos van en ConnectorCredential, nunca aquí.
/// </summary>
public class ConnectorConfiguration
{
    public int IdConfiguracion { get; set; }
    public int IdConnector { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Connector? Connector { get; set; }
}

/// <summary>
/// Credencial del conector. El secreto viaja CIFRADO (ValorCifrado): jamás se
/// persiste en claro. Tipo: None | ApiKey | Basic | Bearer | OAuth2.
/// Para OAuth2 se guardan además cliente y alcance; el token se obtiene en
/// tiempo de ejecución y no se persiste (o se persiste cifrado con expiración).
/// </summary>
public class ConnectorCredential
{
    public int IdCredencial { get; set; }
    public int IdConnector { get; set; }
    public string Tipo { get; set; } = "None";
    public string? NombreUsuario { get; set; }
    public string ValorCifrado { get; set; } = string.Empty;
    public string? ParametrosCifrados { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Connector? Connector { get; set; }
}

/// <summary>
/// Política operativa del conector: Timeout, Retry, Rate Limit y Circuit Breaker.
/// Todo es configurable sin recompilar; deshabilitar el conector corta el tráfico.
/// </summary>
public class ConnectorPolicy
{
    public int IdPolitica { get; set; }
    public int IdConnector { get; set; }
    public int TimeoutSegundos { get; set; } = 30;
    public int MaxReintentos { get; set; } = 2;
    public int IntervaloReintentoMs { get; set; } = 1000;
    public int RateLimitPorMinuto { get; set; } = 60;
    public int CircuitBreakerUmbralFallos { get; set; } = 5;
    public int CircuitBreakerSegundosAbierto { get; set; } = 60;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Connector? Connector { get; set; }
}

/// <summary>
/// Auditoría de cada llamada externa: quién (usuario/agente), qué conector, a
/// dónde (destino SIN secretos), cuánto tardó, estado y resumen de respuesta.
/// Los cuerpos completos no se persisten (solo resumen) para no guardar PII.
/// </summary>
public class ConnectorExecution
{
    public int IdEjecucion { get; set; }
    public int IdConnector { get; set; }
    public int? IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public string? Destino { get; set; }
    public string Estado { get; set; } = "Exitoso";
    public int? CodigoRespuesta { get; set; }
    public long LatenciaMs { get; set; }
    public int Reintentos { get; set; }
    public string? Error { get; set; }
    public string? RespuestaResumen { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Connector? Connector { get; set; }
}
