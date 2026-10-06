using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

/// <summary>Solicitud de ejecución contra un conector externo vía el Gateway.</summary>
public class ConnectorRequest
{
    public string CodigoConector { get; set; } = string.Empty;
    public string Operacion { get; set; } = string.Empty;
    public string? Recurso { get; set; }
    public string Metodo { get; set; } = "GET";
    public string? Cuerpo { get; set; }
    public string? TipoContenido { get; set; }
    public Dictionary<string, string> Cabeceras { get; set; } = new();
    public Dictionary<string, string> Parametros { get; set; } = new();
    public int IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
}

/// <summary>Resultado de una ejecución contra un conector externo.</summary>
public class ConnectorResult
{
    public bool Exitoso { get; set; }
    public int? CodigoRespuesta { get; set; }
    public string? Contenido { get; set; }
    public string? TipoContenido { get; set; }
    public string? Error { get; set; }
    public long LatenciaMs { get; set; }
    public int Reintentos { get; set; }
}

/// <summary>
/// Contrato base de todo conector externo (ETAPA 20). Desacoplado del Gateway:
/// cada implementación solo sabe hablar su protocolo.
/// </summary>
public interface IConnector
{
    /// <summary>Tipos que atiende (REST, SOAP, SMTP, Webhook, Microsoft365, SharePoint).</summary>
    string Tipo { get; }
    Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default);
    Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default);
}

/// <summary>Gateway Empresarial: única puerta de salida hacia sistemas externos.</summary>
public interface IConnectorGateway
{
    Task<ConnectorResult> EjecutarAsync(ConnectorRequest request, CancellationToken ct = default);
}

/// <summary>Cifrado simétrico para secretos de conectores (misma técnica que IConexionCifrador).</summary>
public interface ICredencialCifrador
{
    string Cifrar(string textoPlano);
    string Descifrar(string textoCifrado);
}
