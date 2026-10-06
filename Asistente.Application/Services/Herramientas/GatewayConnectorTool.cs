using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta del Motor (ETAPA 20): los pasos Tool del Planner y los agentes
/// consumen sistemas externos ÚNICAMENTE a través del API Gateway. Parámetros:
/// conector (código, obligatorio), operacion, recurso, metodo (GET por defecto),
/// cuerpo, tipoContenido.
/// Si recurso/metodo/operacion no vienen, se toman de los defectos declarados
/// por el propio conector (RecursoDefecto/MetodoDefecto/OperacionDefecto): el
/// planner nunca inventa rutas, el admin las configura.
/// </summary>
public class GatewayConnectorTool : ITool
{
    public string Name => "GatewayConnectorTool";
    public string Description => "Consume un sistema externo (ERP, CRM, correo, SharePoint, API REST/SOAP) a través del API Gateway Empresarial. Parámetros: conector (código registrado), operacion, recurso (ruta o destinatario), metodo (GET/POST/PUT/DELETE/PATCH), cuerpo.";
    public string Categoria => "Integracion";

    private readonly IConnectorGateway _gateway;
    private readonly IConnectorRepository _repo;

    public GatewayConnectorTool(IConnectorGateway gateway, IConnectorRepository repo)
    {
        _gateway = gateway;
        _repo = repo;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var parametros = request.Parametros ?? new Dictionary<string, object?>();
        var codigo = Valor(parametros, "conector");
        if (string.IsNullOrWhiteSpace(codigo))
            return new ToolExecutionResult { Exitoso = false, Error = "Falta el parámetro 'conector' (código registrado en el Gateway)." };

        var operacion = Valor(parametros, "operacion");
        var recurso = Valor(parametros, "recurso");
        var metodo = Valor(parametros, "metodo");
        if (string.IsNullOrWhiteSpace(recurso) || string.IsNullOrWhiteSpace(metodo) || string.IsNullOrWhiteSpace(operacion))
        {
            var conector = await _repo.GetByCodigoAsync(codigo, cancellationToken);
            string? Defecto(string clave) => conector?.Configuraciones
                .FirstOrDefault(c => c.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase))?.Valor;
            operacion ??= Defecto("OperacionDefecto");
            recurso ??= Defecto("RecursoDefecto");
            metodo ??= Defecto("MetodoDefecto") ?? "GET";
        }

        var resultado = await _gateway.EjecutarAsync(new ConnectorRequest
        {
            CodigoConector = codigo,
            Operacion = operacion ?? string.Empty,
            Recurso = recurso,
            Metodo = metodo ?? "GET",
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
