using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Conectores;

/// <summary>Conector REST genérico (GET, POST, PUT, DELETE, PATCH). Sirve para ERP,
/// CRM y cualquier servicio REST corporativo configurando BaseUrl + credencial.</summary>
public class RestConnector : HttpConnectorBase
{
    public override string Tipo => "REST";

    public RestConnector(ConnectorPolicyState estado, ICredencialCifrador cifrador,
        ILogger<RestConnector> logger, HttpMessageHandler? handler = null)
        : base(estado, cifrador, logger, handler) { }

    public override async Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = Config(conector, "BaseUrl");
            if (string.IsNullOrWhiteSpace(baseUrl))
                return (false, "Falta la configuración 'BaseUrl'.");
            using var cliente = CrearCliente();
            using var mensaje = new HttpRequestMessage(HttpMethod.Get, baseUrl);
            await AplicarAutenticacionAsync(conector, mensaje, ct);
            using var resp = await cliente.SendAsync(mensaje, HttpCompletionOption.ResponseHeadersRead, ct);
            return resp.IsSuccessStatusCode
                ? (true, null)
                : (false, $"HTTP {(int)resp.StatusCode} al probar la conexión.");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "RestConnector: fallo la prueba de conexión del conector '{Codigo}'.", conector.Codigo);
            return (false, ex.Message);
        }
    }

    public override async Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default)
    {
        var baseUrl = Config(conector, "BaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new ConnectorResult { Exitoso = false, Error = "Falta la configuración 'BaseUrl'." };

        var metodo = new HttpMethod((request.Metodo ?? "GET").ToUpperInvariant() switch
        {
            "POST" => "POST",
            "PUT" => "PUT",
            "DELETE" => "DELETE",
            "PATCH" => "PATCH",
            _ => "GET"
        });

        var url = baseUrl.TrimEnd('/') + "/" + (request.Recurso ?? string.Empty).TrimStart('/');
        if (request.Parametros.Count > 0 && (metodo == HttpMethod.Get || metodo == HttpMethod.Delete))
        {
            var query = string.Join("&", request.Parametros
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            url += (url.Contains('?') ? "&" : "?") + query;
        }

        using var cliente = CrearCliente();
        using var mensaje = new HttpRequestMessage(metodo, url);
        await AplicarAutenticacionAsync(conector, mensaje, ct);
        foreach (var h in request.Cabeceras)
            mensaje.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (!string.IsNullOrEmpty(request.Cuerpo) && metodo != HttpMethod.Get && metodo != HttpMethod.Delete)
            mensaje.Content = new StringContent(request.Cuerpo, Encoding.UTF8,
                string.IsNullOrWhiteSpace(request.TipoContenido) ? "application/json" : request.TipoContenido);

        using var respuesta = await cliente.SendAsync(mensaje, ct);
        return await LeerRespuestaAsync(respuesta, ct);
    }
}

/// <summary>Conector SOAP: envía un envelope al endpoint y devuelve el XML crudo.</summary>
public class SoapConnector : HttpConnectorBase
{
    public override string Tipo => "SOAP";

    public SoapConnector(ConnectorPolicyState estado, ICredencialCifrador cifrador,
        ILogger<SoapConnector> logger, HttpMessageHandler? handler = null)
        : base(estado, cifrador, logger, handler) { }

    public override async Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default)
    {
        try
        {
            var endpoint = Config(conector, "Endpoint", Config(conector, "Wsdl"));
            if (string.IsNullOrWhiteSpace(endpoint))
                return (false, "Falta la configuración 'Endpoint' (o 'Wsdl').");
            using var cliente = CrearCliente();
            using var resp = await cliente.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, ct);
            return (int)resp.StatusCode < 500 ? (true, null) : (false, $"HTTP {(int)resp.StatusCode}.");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "SoapConnector: fallo la prueba de conexión del conector '{Codigo}'.", conector.Codigo);
            return (false, ex.Message);
        }
    }

    public override async Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default)
    {
        var endpoint = Config(conector, "Endpoint");
        if (string.IsNullOrWhiteSpace(endpoint))
            return new ConnectorResult { Exitoso = false, Error = "Falta la configuración 'Endpoint'." };
        var cuerpoInterior = request.Cuerpo ?? string.Empty;
        var sobre = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">"
            + "<soap:Body>" + cuerpoInterior + "</soap:Body></soap:Envelope>";

        using var cliente = CrearCliente();
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, endpoint);
        await AplicarAutenticacionAsync(conector, mensaje, ct);
        var prefijo = Config(conector, "SoapActionPrefijo", string.Empty);
        if (!string.IsNullOrWhiteSpace(request.Operacion))
            mensaje.Headers.TryAddWithoutValidation("SOAPAction", prefijo + request.Operacion);
        mensaje.Content = new StringContent(sobre, Encoding.UTF8, "text/xml");
        using var respuesta = await cliente.SendAsync(mensaje, ct);
        return await LeerRespuestaAsync(respuesta, ct);
    }
}

/// <summary>Conector Webhook: publica un evento JSON firmado con HMAC-SHA256.</summary>
public class WebhookConnector : HttpConnectorBase
{
    public override string Tipo => "Webhook";

    public WebhookConnector(ConnectorPolicyState estado, ICredencialCifrador cifrador,
        ILogger<WebhookConnector> logger, HttpMessageHandler? handler = null)
        : base(estado, cifrador, logger, handler) { }

    public override async Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default)
    {
        var url = Config(conector, "Url");
        return string.IsNullOrWhiteSpace(url)
            ? (false, "Falta la configuración 'Url'.")
            : await Task.FromResult((true, (string?)null));
    }

    public override async Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default)
    {
        var url = Config(conector, "Url");
        if (string.IsNullOrWhiteSpace(url))
            return new ConnectorResult { Exitoso = false, Error = "Falta la configuración 'Url'." };

        var cuerpo = request.Cuerpo ?? "{}";
        using var cliente = CrearCliente();
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, url);
        var cred = conector.Credenciales.FirstOrDefault();
        var tipo = (cred?.Tipo ?? "None").Trim();
        if (!tipo.Equals("None", StringComparison.OrdinalIgnoreCase) && cred != null)
        {
            var secreto = Cifrador.Descifrar(cred.ValorCifrado);
            mensaje.Headers.TryAddWithoutValidation(
                Config(conector, "FirmaHeader", "X-Firma"), FirmarHmac(secreto, cuerpo));
        }
        mensaje.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        using var respuesta = await cliente.SendAsync(mensaje, ct);
        return await LeerRespuestaAsync(respuesta, ct);
    }
}
