using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Conectores;

/// <summary>
/// Base de los conectores HTTP (REST, SOAP, Microsoft 365, SharePoint, Webhook).
/// Aplica la autenticación configurada (None | ApiKey | Basic | Bearer | OAuth2)
/// y expone utilidades de configuración. Los secretos siempre se descifran en
/// memoria y jamás se registran en logs ni en auditoría.
/// </summary>
public abstract class HttpConnectorBase : IConnector
{
    public abstract string Tipo { get; }

    protected readonly ConnectorPolicyState Estado;
    protected readonly ICredencialCifrador Cifrador;
    protected readonly ILogger Logger;
    private readonly HttpMessageHandler? _handler;

    protected HttpConnectorBase(
        ConnectorPolicyState estado,
        ICredencialCifrador cifrador,
        ILogger logger,
        HttpMessageHandler? handler = null)
    {
        Estado = estado;
        Cifrador = cifrador;
        Logger = logger;
        _handler = handler;
    }

    public abstract Task<(bool Ok, string? Error)> ProbarConexionAsync(Connector conector, CancellationToken ct = default);
    public abstract Task<ConnectorResult> EjecutarAsync(Connector conector, ConnectorRequest request, CancellationToken ct = default);

    protected static string Config(Connector conector, string clave, string defecto = "")
        => conector.Configuraciones.FirstOrDefault(c =>
               c.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase))?.Valor ?? defecto;

    protected HttpClient CrearCliente()
        => _handler != null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();

    /// <summary>Aplica la credencial del conector al request saliente.</summary>
    protected async Task<string?> AplicarAutenticacionAsync(
        Connector conector, HttpRequestMessage mensaje, CancellationToken ct)
    {
        var cred = conector.Credenciales.FirstOrDefault();
        var tipo = (cred?.Tipo ?? "None").Trim();
        if (tipo.Equals("None", StringComparison.OrdinalIgnoreCase) || cred == null)
            return null;

        if (tipo.Equals("ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            var secreto = Cifrador.Descifrar(cred.ValorCifrado);
            var header = Config(conector, "ApiKeyHeader", "X-API-Key");
            var query = Config(conector, "ApiKeyQuery", string.Empty);
            if (!string.IsNullOrWhiteSpace(query) && mensaje.RequestUri != null)
            {
                var ub = new UriBuilder(mensaje.RequestUri);
                ub.Query = string.IsNullOrEmpty(ub.Query)
                    ? $"{query}={Uri.EscapeDataString(secreto)}"
                    : ub.Query.TrimStart('?') + $"&{query}={Uri.EscapeDataString(secreto)}";
                mensaje.RequestUri = ub.Uri;
            }
            else
            {
                mensaje.Headers.TryAddWithoutValidation(header, secreto);
            }
            return null;
        }

        if (tipo.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            var secreto = Cifrador.Descifrar(cred.ValorCifrado);
            var usuario = cred.NombreUsuario ?? string.Empty;
            var basico = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{secreto}"));
            mensaje.Headers.Authorization = new AuthenticationHeaderValue("Basic", basico);
            return null;
        }

        if (tipo.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            mensaje.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", Cifrador.Descifrar(cred.ValorCifrado));
            return null;
        }

        if (tipo.Equals("OAuth2", StringComparison.OrdinalIgnoreCase))
        {
            var token = await ObtenerTokenOAuth2Async(conector, cred, ct);
            mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return null;
        }

        throw new InvalidOperationException($"Tipo de credencial '{tipo}' no soportado.");
    }

    /// <summary>OAuth2 client_credentials con caché en memoria (nunca se persiste el token).</summary>
    protected async Task<string> ObtenerTokenOAuth2Async(
        Connector conector, ConnectorCredential cred, CancellationToken ct)
    {
        var claveCache = $"oauth:{conector.IdConnector}";
        if (Estado.TryGetToken(claveCache, out var cacheado))
            return cacheado;

        var tokenUrl = Config(conector, "TokenUrl");
        if (string.IsNullOrWhiteSpace(tokenUrl))
            throw new InvalidOperationException("Falta la configuración 'TokenUrl' para OAuth2.");
        var parametros = LeerParametros(cred);
        parametros.TryGetValue("client_id", out var clientId);
        clientId ??= cred.NombreUsuario ?? string.Empty;
        var secreto = Cifrador.Descifrar(cred.ValorCifrado);
        parametros.TryGetValue("scope", out var scope);

        using var cliente = CrearCliente();
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secreto,
            ["scope"] = scope ?? string.Empty
        });
        using var resp = await cliente.PostAsync(tokenUrl, contenido, ct);
        var cuerpo = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OAuth2 devolvió {(int)resp.StatusCode}: {Recortar(cuerpo, 300)}.");

        using var doc = JsonDocument.Parse(cuerpo);
        if (!doc.RootElement.TryGetProperty("access_token", out var tk))
            throw new InvalidOperationException("OAuth2 no devolvió access_token.");
        var expira = doc.RootElement.TryGetProperty("expires_in", out var ex) && ex.TryGetInt32(out var s) ? s : 3600;
        var token = tk.GetString() ?? string.Empty;
        Estado.GuardarToken(claveCache, token, expira);
        return token;
    }

    protected Dictionary<string, string> LeerParametros(ConnectorCredential cred)
    {
        var json = Cifrador.Descifrar(cred.ParametrosCifrados ?? string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
    }

    protected static async Task<ConnectorResult> LeerRespuestaAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var contenido = await resp.Content.ReadAsStringAsync(ct);
        return new ConnectorResult
        {
            Exitoso = resp.IsSuccessStatusCode,
            CodigoRespuesta = (int)resp.StatusCode,
            Contenido = contenido,
            TipoContenido = resp.Content.Headers.ContentType?.ToString(),
            Error = resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}: {Recortar(contenido, 300)}"
        };
    }

    protected static string Recortar(string texto, int max)
        => string.IsNullOrEmpty(texto) || texto.Length <= max ? texto : texto[..max];

    protected static string FirmarHmac(string secreto, string contenido)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(contenido))).ToLowerInvariant();
    }
}
