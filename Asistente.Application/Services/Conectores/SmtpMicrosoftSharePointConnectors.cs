using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Conectores;

/// <summary>Conector SMTP: envía correo (reportes, enlaces, alertas). Config: Host,
/// Puerto, SSL, Remitente. La contraseña viaja cifrada. Para pruebas sin servidor
/// se puede configurar 'PickupDirectory' y el mensaje se escribe a disco.</summary>
public class SmtpConnector : IConnector
{
    public string Tipo => "SMTP";

    private readonly ICredencialCifrador _cifrador;
    private readonly ILogger<SmtpConnector> _logger;

    public SmtpConnector(ICredencialCifrador cifrador, ILogger<SmtpConnector> logger)
    {
        _cifrador = cifrador;
        _logger = logger;
    }

    public Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default)
    {
        var host = Config(conector, "Host");
        if (string.IsNullOrWhiteSpace(host))
            return Task.FromResult((false, "Falta la configuración 'Host'."));
        if (!string.IsNullOrWhiteSpace(Config(conector, "PickupDirectory")))
            return Task.FromResult((true, (string?)null));
        try
        {
            using var cliente = CrearCliente(conector);
            return Task.FromResult((true, (string?)null));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmtpConnector: fallo la prueba de conexión del conector '{Codigo}'.", conector.Codigo);
            return Task.FromResult((false, ex.Message));
        }
    }

    public async Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default)
    {
        var inicio = DateTime.UtcNow;
        try
        {
            var para = request.Recurso ?? request.Parametros.GetValueOrDefault("Para") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(para))
                return new ConnectorResult { Exitoso = false, Error = "Falta el destinatario (Recurso o parámetro 'Para')." };
            request.Parametros.TryGetValue("Asunto", out var asunto);
            request.Parametros.TryGetValue("EsHtml", out var esHtml);

            using var mensaje = new MailMessage
            {
                From = new MailAddress(Config(conector, "Remitente", "asistente@empresa.local")),
                Subject = string.IsNullOrWhiteSpace(asunto) ? request.Operacion : asunto,
                Body = request.Cuerpo ?? string.Empty,
                IsBodyHtml = esHtml?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
            };
            foreach (var destino in para.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                mensaje.To.Add(destino);

            using var cliente = CrearCliente(conector);
            await cliente.SendMailAsync(mensaje, ct);
            return new ConnectorResult
            {
                Exitoso = true,
                CodigoRespuesta = 250,
                Contenido = $"Correo enviado a {para}.",
                LatenciaMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmtpConnector: fallo el envío del conector '{Codigo}'.", conector.Codigo);
            return new ConnectorResult
            {
                Exitoso = false,
                Error = ex.Message,
                LatenciaMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds
            };
        }
    }

    private SmtpClient CrearCliente(Connector conector)
    {
        var pickup = Config(conector, "PickupDirectory");
        var cliente = new SmtpClient
        {
            DeliveryMethod = string.IsNullOrWhiteSpace(pickup)
                ? SmtpDeliveryMethod.Network
                : SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickup,
            Host = Config(conector, "Host", "localhost"),
            Port = int.TryParse(Config(conector, "Puerto", "25"), out var p) ? p : 25,
            EnableSsl = Config(conector, "SSL", "false").Equals("true", StringComparison.OrdinalIgnoreCase)
        };
        var cred = conector.Credenciales.FirstOrDefault();
        if (cred != null && !cred.Tipo.Equals("None", StringComparison.OrdinalIgnoreCase))
            cliente.Credentials = new NetworkCredential(
                cred.NombreUsuario ?? string.Empty, _cifrador.Descifrar(cred.ValorCifrado));
        return cliente;
    }

    private static string Config(Connector conector, string clave, string defecto = "")
        => conector.Configuraciones.FirstOrDefault(c =>
               c.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase))?.Valor ?? defecto;
}

/// <summary>Conector Microsoft 365: OAuth2 client_credentials + Microsoft Graph.</summary>
public class Microsoft365Connector : HttpConnectorBase
{
    public override string Tipo => "Microsoft365";

    public Microsoft365Connector(ConnectorPolicyState estado, ICredencialCifrador cifrador,
        ILogger<Microsoft365Connector> logger, HttpMessageHandler? handler = null)
        : base(estado, cifrador, logger, handler) { }

    public override async Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default)
    {
        try
        {
            await ObtenerTokenOAuth2Async(conector,
                conector.Credenciales.FirstOrDefault() ?? new ConnectorCredential { Tipo = "OAuth2" }, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Microsoft365Connector: fallo la prueba de conexión del conector '{Codigo}'.", conector.Codigo);
            return (false, ex.Message);
        }
    }

    public override async Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default)
    {
        var baseUrl = Config(conector, "BaseUrl", "https://graph.microsoft.com/v1.0");
        var url = baseUrl.TrimEnd('/') + "/" + (request.Recurso ?? string.Empty).TrimStart('/');
        using var cliente = CrearCliente();
        using var mensaje = new HttpRequestMessage(
            string.Equals(request.Metodo, "POST", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post : HttpMethod.Get, url);
        await AplicarAutenticacionAsync(conector, mensaje, ct);
        if (!string.IsNullOrEmpty(request.Cuerpo))
            mensaje.Content = new StringContent(request.Cuerpo, System.Text.Encoding.UTF8,
                string.IsNullOrWhiteSpace(request.TipoContenido) ? "application/json" : request.TipoContenido);
        using var respuesta = await cliente.SendAsync(mensaje, ct);
        return await LeerRespuestaAsync(respuesta, ct);
    }
}

/// <summary>Conector SharePoint: OAuth2 + SharePoint REST (_api). Config: SiteUrl.</summary>
public class SharePointConnector : HttpConnectorBase
{
    public override string Tipo => "SharePoint";

    public SharePointConnector(ConnectorPolicyState estado, ICredencialCifrador cifrador,
        ILogger<SharePointConnector> logger, HttpMessageHandler? handler = null)
        : base(estado, cifrador, logger, handler) { }

    public override async Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default)
    {
        try
        {
            var site = Config(conector, "SiteUrl");
            if (string.IsNullOrWhiteSpace(site))
                return (false, "Falta la configuración 'SiteUrl'.");
            await ObtenerTokenOAuth2Async(conector,
                conector.Credenciales.FirstOrDefault() ?? new ConnectorCredential { Tipo = "OAuth2" }, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "SharePointConnector: fallo la prueba de conexión del conector '{Codigo}'.", conector.Codigo);
            return (false, ex.Message);
        }
    }

    public override async Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default)
    {
        var site = Config(conector, "SiteUrl");
        if (string.IsNullOrWhiteSpace(site))
            return new ConnectorResult { Exitoso = false, Error = "Falta la configuración 'SiteUrl'." };
        var url = site.TrimEnd('/') + "/_api/" + (request.Recurso ?? "web").TrimStart('/');
        using var cliente = CrearCliente();
        using var mensaje = new HttpRequestMessage(
            string.Equals(request.Metodo, "POST", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post : HttpMethod.Get, url);
        mensaje.Headers.TryAddWithoutValidation("Accept", "application/json;odata=verbose");
        await AplicarAutenticacionAsync(conector, mensaje, ct);
        if (!string.IsNullOrEmpty(request.Cuerpo))
            mensaje.Content = new StringContent(request.Cuerpo, System.Text.Encoding.UTF8,
                string.IsNullOrWhiteSpace(request.TipoContenido) ? "application/json" : request.TipoContenido);
        using var respuesta = await cliente.SendAsync(mensaje, ct);
        return await LeerRespuestaAsync(respuesta, ct);
    }
}
