using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta del Motor (ETAPA 20): los pasos Tool del Planner y los agentes
/// consumen sistemas externos ÚNICAMENTE a través del API Gateway. Parámetros:
/// conector (código, obligatorio), operacion, recurso, metodo (GET por defecto),
/// cuerpo, tipoContenido.
/// </summary>
public class GatewayConnectorTool : ITool
{
    public string Name => "GatewayConnectorTool";
    public string Description => "Consume un sistema externo (ERP, CRM, correo, SharePoint, API REST/SOAP) a través del API Gateway Empresarial. Parámetros: conector (código registrado), operacion, recurso (ruta o destinatario), metodo (GET/POST/PUT/DELETE/PATCH), cuerpo.";
    public string Categoria => "Integracion";

    private readonly IConnectorGateway _gateway;

    public GatewayConnectorTool(IConnectorGateway gateway) => _gateway = gateway;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var parametros = request.Parametros ?? new Dictionary<string, object?>();
        var codigo = Valor(parametros, "conector");
        if (string.IsNullOrWhiteSpace(codigo))
            return new ToolExecutionResult { Exitoso = false, Error = "Falta el parámetro 'conector' (código registrado en el Gateway)." };

        var resultado = await _gateway.EjecutarAsync(new ConnectorRequest
        {
            CodigoConector = codigo,
            Operacion = Valor(parametros, "operacion") ?? string.Empty,
            Recurso = Valor(parametros, "recurso"),
            Metodo = Valor(parametros, "metodo") ?? "GET",
            Cuerpo = Valor(parametros, "cuerpo"),
            TipoContenido = Valor(parametros, "tipoContenido"),
            IdUsuario = request.IdUsuario,
            IdAsistente = request.IdAsistente
        }, cancellationToken);

        return new ToolExecutionResult
        {
            Exitoso = resultado.Exitoso,
            Contenido = resultado.Contenido,
            Error = resultado.Error,
            Metadatos = new Dictionary<string, object?>
            {
                ["codigoRespuesta"] = resultado.CodigoRespuesta,
                ["latenciaMs"] = resultado.LatenciaMs,
                ["reintentos"] = resultado.Reintentos,
                ["conector"] = codigo
            }
        };
    }

    private static string? Valor(Dictionary<string, object?> parametros, string clave)
        => parametros.TryGetValue(clave, out var v) ? v?.ToString() : null;
}
