using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Conectores;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Services;
using Asistente.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Asistente.Tests.Services;

/// <summary>Pruebas de la ETAPA 20: Gateway, políticas, cifrado y conectores.</summary>
public class ConnectorGatewayTests
{
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _respuestas = new();
        public List<HttpRequestMessage> Recibidas { get; } = new();
        public List<string> CuerposRecibidos { get; } = new();

        public void Encolar(HttpStatusCode codigo, string contenido = "", string tipo = "application/json")
            => _respuestas.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(codigo)
            {
                Content = new StringContent(contenido, System.Text.Encoding.UTF8, tipo)
            }));

        public void Encolar(Func<HttpRequestMessage, HttpResponseMessage> fabrica)
            => _respuestas.Enqueue((req, _) => Task.FromResult(fabrica(req)));

        public void EncolarAsync(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fabrica)
            => _respuestas.Enqueue(fabrica);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Recibidas.Add(request);
            CuerposRecibidos.Add(request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(ct));
            if (_respuestas.Count == 0)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                };
            return await _respuestas.Dequeue()(request, ct);
        }
    }

    private sealed class RepoMemoria : IConnectorRepository
    {
        private readonly Dictionary<string, Connector> _porCodigo = new(StringComparer.OrdinalIgnoreCase);
        public void Agregar(Connector c) => _porCodigo[c.Codigo] = c;
        public Task<Connector?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_porCodigo.Values.FirstOrDefault(c => c.IdConnector == id));
        public Task<Connector?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
            => Task.FromResult(_porCodigo.TryGetValue(codigo, out var c) ? c : null);
        public Task<List<Connector>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(_porCodigo.Values.ToList());
        public Task<Connector> AddAsync(Connector c, CancellationToken ct = default)
        {
            c.IdConnector = _porCodigo.Count + 1;
            _porCodigo[c.Codigo] = c;
            return Task.FromResult(c);
        }
        public Task UpdateAsync(Connector c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ExecMemoria : IConnectorExecutionRepository
    {
        public List<ConnectorExecution> Logs { get; } = new();
        public Task AddAsync(ConnectorExecution e, CancellationToken ct = default)
        {
            Logs.Add(e);
            return Task.CompletedTask;
        }
        public Task<List<ConnectorExecution>> GetByConnectorAsync(int id, int tope = 100, CancellationToken ct = default)
            => Task.FromResult(Logs.Where(l => l.IdConnector == id).Take(tope).ToList());
        public Task<ConnectorMetricas> GetMetricasAsync(int id, CancellationToken ct = default)
            => Task.FromResult(new ConnectorMetricas { IdConnector = id, Total = Logs.Count(l => l.IdConnector == id) });
    }

    private static Mock<IAutorizacionService> Auth(bool permitido)
    {
        var m = new Mock<IAutorizacionService>();
        m.Setup(a => a.VerificarPermisoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = permitido });
        return m;
    }

    private static ICredencialCifrador CifradorPrueba()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Gateway:ClaveCifrado"]).Returns("clave-de-prueba-etapa20");
        return new CredencialCifrador(config.Object);
    }

    private static Connector ConectorRest(
        string codigo = "CRM_DEMO", bool activo = true, bool requierePermiso = false,
        Action<Connector>? ajustar = null)
    {
        var c = new Connector
        {
            IdConnector = 7,
            Codigo = codigo,
            Nombre = "CRM demo",
            Tipo = "REST",
            Activo = activo,
            RequierePermiso = requierePermiso
        };
        c.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "BaseUrl", Valor = "https://crm.demo/api" });
        c.Politicas.Add(new ConnectorPolicy
        {
            TimeoutSegundos = 10, MaxReintentos = 1, IntervaloReintentoMs = 1,
            RateLimitPorMinuto = 1000, CircuitBreakerUmbralFallos = 100, CircuitBreakerSegundosAbierto = 60
        });
        ajustar?.Invoke(c);
        return c;
    }

    private static ConnectorGateway Gateway(
        RepoMemoria repo, ExecMemoria exec, bool permitido = true,
        HttpMessageHandler? handler = null)
    {
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var conectores = new IConnector[]
        {
            new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance, handler),
            new SoapConnector(estado, cifrador, NullLogger<SoapConnector>.Instance, handler),
            new WebhookConnector(estado, cifrador, NullLogger<WebhookConnector>.Instance, handler),
            new SmtpConnector(cifrador, NullLogger<SmtpConnector>.Instance),
            new Microsoft365Connector(estado, cifrador, NullLogger<Microsoft365Connector>.Instance, handler),
            new SharePointConnector(estado, cifrador, NullLogger<SharePointConnector>.Instance, handler)
        };
        return new ConnectorGateway(repo, exec, conectores, estado,
            Auth(permitido).Object, NullLogger<ConnectorGateway>.Instance);
    }

    [Fact]
    public void Cifrador_Roundtrip_NoDejaElSecretoEnClaro()
    {
        var cifrador = CifradorPrueba();
        const string secreto = "super-secreto-123";

        var cifrado = cifrador.Cifrar(secreto);

        Assert.NotEqual(secreto, cifrado);
        Assert.Equal(secreto, cifrador.Descifrar(cifrado));
        Assert.Equal(string.Empty, cifrador.Cifrar(string.Empty));
    }

    [Fact]
    public async Task Gateway_ConectorInexistente_NoLanza()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var gw = Gateway(repo, exec);

        var r = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "NO_EXISTE", Operacion = "x", IdUsuario = 1 });

        Assert.False(r.Exitoso);
        Assert.Contains("No existe", r.Error);
    }

    [Fact]
    public async Task Gateway_Deshabilitado_RechazaYAudita()
    {
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest(activo: false));
        var exec = new ExecMemoria();
        var gw = Gateway(repo, exec);

        var r = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 1 });

        Assert.False(r.Exitoso);
        Assert.Contains("deshabilitado", r.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Deshabilitado", Assert.Single(exec.Logs).Estado);
    }

    [Fact]
    public async Task Gateway_SinPermiso_RechazaAntesDeLlamarFuera()
    {
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest(requierePermiso: true));
        var exec = new ExecMemoria();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, "{}");
        var gw = Gateway(repo, exec, permitido: false, handler: fake);

        var r = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 9 });

        Assert.False(r.Exitoso);
        Assert.Contains("permiso", r.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fake.Recibidas);
        Assert.Equal("SinPermiso", Assert.Single(exec.Logs).Estado);
    }

    [Fact]
    public async Task Gateway_ExitoRest_AuditaConDestinoSinSecretos()
    {
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest());
        var exec = new ExecMemoria();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, """{"id":1}""");
        var gw = Gateway(repo, exec, handler: fake);

        var r = await gw.EjecutarAsync(new ConnectorRequest
        {
            CodigoConector = "CRM_DEMO", Operacion = "Obtener cliente",
            Recurso = "clientes/1?token=SECRETO", Metodo = "GET", IdUsuario = 1, IdAsistente = 2
        });

        Assert.True(r.Exitoso);
        Assert.Equal(200, r.CodigoRespuesta);
        Assert.Contains("id", r.Contenido);
        var log = Assert.Single(exec.Logs);
        Assert.Equal("Exitoso", log.Estado);
        Assert.Equal(7, log.IdConnector);
        Assert.Equal(1, log.IdUsuario);
        Assert.Equal(2, log.IdAsistente);
        Assert.DoesNotContain("SECRETO", log.Destino ?? string.Empty);
    }

    [Fact]
    public async Task Gateway_ReintentaElMismoDestinoYRecupera()
    {
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest());
        var exec = new ExecMemoria();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.InternalServerError, "caído");
        fake.Encolar(HttpStatusCode.OK, """{"ok":true}""");
        var gw = Gateway(repo, exec, handler: fake);

        var r = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", Recurso = "clientes", IdUsuario = 1 });

        Assert.True(r.Exitoso);
        Assert.Equal(1, r.Reintentos);
        Assert.Equal(2, fake.Recibidas.Count);
        Assert.Equal("Exitoso", Assert.Single(exec.Logs).Estado);
    }

    [Fact]
    public async Task Gateway_RateLimit_CortaSinLlamarFuera()
    {
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest(ajustar: c =>
            c.Politicas.First().RateLimitPorMinuto = 1));
        var exec = new ExecMemoria();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, "{}");
        fake.Encolar(HttpStatusCode.OK, "{}");
        var gw = Gateway(repo, exec, handler: fake);

        var primero = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 1 });
        var segundo = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 1 });

        Assert.True(primero.Exitoso);
        Assert.False(segundo.Exitoso);
        Assert.Contains("mite", segundo.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, fake.Recibidas.Count);
    }

    [Fact]
    public async Task Gateway_CircuitBreaker_AbortaRapidoTrasUmbral()
    {
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest(ajustar: c =>
        {
            var p = c.Politicas.First();
            p.MaxReintentos = 0;
            p.CircuitBreakerUmbralFallos = 1;
            p.CircuitBreakerSegundosAbierto = 600;
        }));
        var exec = new ExecMemoria();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.InternalServerError, "boom");
        var gw = Gateway(repo, exec, handler: fake);

        var primero = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 1 });
        var segundo = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 1 });

        Assert.False(primero.Exitoso);
        Assert.False(segundo.Exitoso);
        Assert.Contains("Circuito", segundo.Error);
        Assert.Equal(1, fake.Recibidas.Count);
        Assert.Equal("CircuitoAbierto", exec.Logs.Last().Estado);
    }

    [Fact]
    public async Task Gateway_Timeout_RespetaLaPolitica()
    {
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest(ajustar: c =>
        {
            var p = c.Politicas.First();
            p.TimeoutSegundos = 1;
            p.MaxReintentos = 0;
        }));
        var exec = new ExecMemoria();
        var fake = new FakeHttpHandler();
        fake.EncolarAsync(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var gw = Gateway(repo, exec, handler: fake);

        var r = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 1 });

        Assert.False(r.Exitoso);
        Assert.Contains("timeout", r.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestConnector_Probar_UsaRecursoDefecto_EnVezDeLaRaiz()
    {
        // Muchas APIs (incluida la nuestra) devuelven 404 en "/": el probe usa el
        // RecursoDefecto si existe, que es la llamada real del conector.
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, "{}");
        var rest = new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance, fake);

        var conector = ConectorRest();
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "RecursoDefecto", Valor = "api/health" });

        var (ok, error) = await rest.ProbarConexionAsync(conector);

        Assert.True(ok, error);
        Assert.EndsWith("/api/health",
            Assert.Single(fake.Recibidas).RequestUri?.ToString());
    }

    [Fact]
    public async Task RestConnector_AplicaApiKeyEnCabecera()
    {
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, "{}");
        var rest = new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance, fake);

        var conector = ConectorRest();
        conector.Credenciales.Add(new ConnectorCredential
            { Tipo = "ApiKey", ValorCifrado = cifrador.Cifrar("clave-123") });
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "ApiKeyHeader", Valor = "X-Clave" });

        var r = await rest.EjecutarAsync(conector,
            new ConnectorRequest { CodigoConector = "CRM_DEMO", Recurso = "ping" });

        Assert.True(r.Exitoso);
        var enviado = Assert.Single(fake.Recibidas);
        Assert.Equal("clave-123", string.Join(",", enviado.Headers.GetValues("X-Clave")));
        Assert.StartsWith("https://crm.demo/api/ping", enviado.RequestUri?.ToString());
    }

    [Fact]
    public async Task RestConnector_OAuth2_ObtieneTokenYLoReutiliza()
    {
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, """{"access_token":"tok-1","expires_in":3600}""");
        fake.Encolar(HttpStatusCode.OK, """{"data":[]}""");
        fake.Encolar(HttpStatusCode.OK, """{"data":[]}""");
        var rest = new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance, fake);

        var conector = ConectorRest("M365");
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "TokenUrl", Valor = "https://login/token" });
        conector.Credenciales.Add(new ConnectorCredential
        {
            Tipo = "OAuth2", NombreUsuario = "cliente-1",
            ValorCifrado = cifrador.Cifrar("secreto-1")
        });
        var req = new ConnectorRequest { CodigoConector = "M365", Recurso = "users" };

        await rest.EjecutarAsync(conector, req);
        await rest.EjecutarAsync(conector, req);

        // 1 llamada al token + 2 al recurso: el token se reutiliza, no se pide dos veces.
        Assert.Equal(3, fake.Recibidas.Count);
        Assert.Contains("login/token", fake.Recibidas[0].RequestUri?.ToString());
        Assert.Equal("Bearer", fake.Recibidas[1].Headers.Authorization?.Scheme);
        Assert.Equal("tok-1", fake.Recibidas[1].Headers.Authorization?.Parameter);
        Assert.Equal("Bearer", fake.Recibidas[2].Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task SoapConnector_EnvuelveEnSobre()
    {
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, "<ok/>", "text/xml");
        var soap = new SoapConnector(estado, cifrador, NullLogger<SoapConnector>.Instance, fake);

        var conector = ConectorRest("ERP_SOAP");
        conector.Tipo = "SOAP";
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "Endpoint", Valor = "https://erp.demo/soap" });

        var r = await soap.EjecutarAsync(conector, new ConnectorRequest
        {
            CodigoConector = "ERP_SOAP", Operacion = "ObtenerCliente",
            Cuerpo = "<cli><id>1</id></cli>"
        });

        Assert.True(r.Exitoso);
        var enviado = Assert.Single(fake.Recibidas);
        var xml = Assert.Single(fake.CuerposRecibidos);
        Assert.Contains("soap:Envelope", xml);
        Assert.Contains("<cli><id>1</id></cli>", xml);
        Assert.Contains("ObtenerCliente", enviado.Headers.GetValues("SOAPAction"));
    }

    [Fact]
    public async Task WebhookConnector_FirmaElCuerpoConHmac()
    {
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, "recibido");
        var wh = new WebhookConnector(estado, cifrador, NullLogger<WebhookConnector>.Instance, fake);

        var conector = ConectorRest("WH");
        conector.Tipo = "Webhook";
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "Url", Valor = "https://hooks.demo/e" });
        conector.Credenciales.Add(new ConnectorCredential
            { Tipo = "Bearer", ValorCifrado = cifrador.Cifrar("firma-secreta") });

        var r = await wh.EjecutarAsync(conector, new ConnectorRequest
            { CodigoConector = "WH", Cuerpo = """{"evento":"x"}""" });

        Assert.True(r.Exitoso);
        var enviado = Assert.Single(fake.Recibidas);
        Assert.True(enviado.Headers.Contains("X-Firma"));
        Assert.Equal(64, string.Join("", enviado.Headers.GetValues("X-Firma")).Length);
    }

    [Fact]
    public async Task SmtpConnector_EnviaSinServidor_ConPickupDirectory()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "smtp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        try
        {
            var cifrador = CifradorPrueba();
            var smtp = new SmtpConnector(cifrador, NullLogger<SmtpConnector>.Instance);
            var conector = ConectorRest("SMTP");
            conector.Tipo = "SMTP";
            conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
                { Clave = "PickupDirectory", Valor = raiz });

            var r = await smtp.EjecutarAsync(conector, new ConnectorRequest
            {
                CodigoConector = "SMTP",
                Recurso = "a@demo.local",
                Operacion = "prueba",
                Cuerpo = "hola",
                IdUsuario = 1,
                Parametros = new Dictionary<string, string> { ["Asunto"] = "prueba" }
            });

            Assert.True(r.Exitoso);
            Assert.Single(Directory.GetFiles(raiz, "*.eml"));
        }
        finally { Directory.Delete(raiz, true); }
    }

    [Fact]
    public void SmtpConnector_RutaAdjunto_SoloCarpetasPermitidasYExtensionSegura()
    {
        Assert.False(SmtpConnector.EsRutaAdjuntoPermitida("/etc/passwd"));
        Assert.False(SmtpConnector.EsRutaAdjuntoPermitida("/AsistenteIA_Documentos/../../etc/passwd"));
        Assert.False(SmtpConnector.EsRutaAdjuntoPermitida("/AsistenteIA_Documentos/reporte.exe"));
        Assert.False(SmtpConnector.EsRutaAdjuntoPermitida("C:\\Windows\\System32\\cmd.exe"));
        Assert.True(SmtpConnector.EsRutaAdjuntoPermitida(
            Path.Combine(Path.GetTempPath(), "reporte.pdf")));
    }

    [Fact]
    public async Task SmtpConnector_AdjuntoInexistente_RechazaSinEnviar()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "smtp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        try
        {
            var cifrador = CifradorPrueba();
            var smtp = new SmtpConnector(cifrador, NullLogger<SmtpConnector>.Instance);
            var conector = ConectorRest("SMTP2");
            conector.Tipo = "SMTP";
            conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
                { Clave = "PickupDirectory", Valor = raiz });

            var r = await smtp.EjecutarAsync(conector, new ConnectorRequest
            {
                CodigoConector = "SMTP2",
                Recurso = "a@demo.local",
                Cuerpo = "x",
                Parametros = new Dictionary<string, string>
                    { ["Adjuntos"] = Path.Combine(raiz, "no-existe.pdf") }
            });

            Assert.False(r.Exitoso);
            Assert.Contains("Adjunto", r.Error);
            Assert.Empty(Directory.GetFiles(raiz));
        }
        finally { Directory.Delete(raiz, true); }
    }

    [Fact]
    public async Task SharePointConnector_ConstruyeUrlApi()
    {
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, """{"d":{}}""");
        var sp = new SharePointConnector(estado, cifrador, NullLogger<SharePointConnector>.Instance, fake);

        var conector = ConectorRest("SP");
        conector.Tipo = "SharePoint";
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "SiteUrl", Valor = "https://contoso.sharepoint.com/sites/demo" });

        var r = await sp.EjecutarAsync(conector, new ConnectorRequest
            { CodigoConector = "SP", Recurso = "web/lists" });

        Assert.True(r.Exitoso);
        var enviado = Assert.Single(fake.Recibidas);
        Assert.StartsWith(
            "https://contoso.sharepoint.com/sites/demo/_api/web/lists",
            enviado.RequestUri?.ToString());
    }

    [Fact]
    public async Task ConnectorService_ProbarConexion_QuedaAuditado()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.OK, "{}");
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance, fake) });
        await servicio.RegistrarAsync(new CrearConectorRequest
        {
            Codigo = "P", Nombre = "P", Tipo = "REST",
            Configuracion = new Dictionary<string, string> { ["BaseUrl"] = "https://x" }
        });

        var (ok, _) = await servicio.ProbarConexionAsync("P");

        Assert.True(ok);
        var log = Assert.Single(exec.Logs);
        Assert.Equal("ProbarConexion", log.Operacion);
        Assert.Equal("Exitoso", log.Estado);
    }

    [Fact]
    public async Task ConnectorService_ActualizarCredencial_RotaElSecretoSinExponerlo()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance) });
        await servicio.RegistrarAsync(new CrearConectorRequest
        {
            Codigo = "ROT", Nombre = "ROT", Tipo = "REST",
            Credencial = new CrearCredencialRequest { Tipo = "ApiKey", Secreto = "vieja" }
        });

        var dto = await servicio.ActualizarCredencialAsync("ROT",
            new CrearCredencialRequest { Tipo = "ApiKey", Secreto = "nueva" });

        Assert.True(dto.TieneCredencial);
        var guardado = await repo.GetByCodigoAsync("ROT");
        var cred = Assert.Single(guardado!.Credenciales);
        Assert.Equal("nueva", cifrador.Descifrar(cred.ValorCifrado));
        var serializado = System.Text.Json.JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("nueva", serializado);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servicio.ActualizarCredencialAsync("NO_EXISTE",
                new CrearCredencialRequest { Tipo = "ApiKey", Secreto = "x" }));
    }

    [Fact]
    public async Task ConnectorService_ActualizarConfiguracion_MezclaSinBorrar()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance) });
        await servicio.RegistrarAsync(new CrearConectorRequest
        {
            Codigo = "CFG", Nombre = "CFG", Tipo = "REST",
            Configuracion = new Dictionary<string, string>
                { ["BaseUrl"] = "https://a", ["RecursoDefecto"] = "x" }
        });

        var dto = await servicio.ActualizarConfiguracionAsync("CFG",
            new Dictionary<string, string>
            {
                ["RecursoDefecto"] = "y",
                ["RecursoDefecto_POST"] = "z"
            });

        Assert.Equal("y", dto.Configuracion["RecursoDefecto"]);
        Assert.Equal("z", dto.Configuracion["RecursoDefecto_POST"]);
        Assert.Equal("https://a", dto.Configuracion["BaseUrl"]);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servicio.ActualizarConfiguracionAsync("NOPE", new Dictionary<string, string>()));
    }

    [Fact]
    public async Task ConnectorService_ActualizarConfiguracion_ValorVacio_Elimina()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance) });
        await servicio.RegistrarAsync(new CrearConectorRequest
        {
            Codigo = "DEL", Nombre = "DEL", Tipo = "REST",
            Configuracion = new Dictionary<string, string>
                { ["BaseUrl"] = "https://a", ["PickupDirectory"] = "/tmp/x" }
        });

        var dto = await servicio.ActualizarConfiguracionAsync("DEL",
            new Dictionary<string, string> { ["PickupDirectory"] = "" });

        Assert.False(dto.Configuracion.ContainsKey("PickupDirectory"));
        Assert.Equal("https://a", dto.Configuracion["BaseUrl"]);
    }

    [Fact]
    public async Task ConnectorService_ActualizarDatos_CambiaNombreYPermiso()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance) });
        await servicio.RegistrarAsync(new CrearConectorRequest
            { Codigo = "D", Nombre = "Viejo", Tipo = "REST" });

        var dto = await servicio.ActualizarDatosAsync("D", "Nuevo", "desc", false);

        Assert.Equal("Nuevo", dto.Nombre);
        Assert.Equal("desc", (await repo.GetByCodigoAsync("D"))!.Descripcion);
        Assert.False((await repo.GetByCodigoAsync("D"))!.RequierePermiso);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ActualizarDatosAsync("D", "  ", null, false));
    }

    [Fact]
    public async Task ConnectorService_ActualizarCredencial_SinSecreto_ConservaYCambiaTipoUsuario()
    {
        // Bug real: cambiar solo usuario/tipo (secreto vacío) se ignoraba entero.
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance) });
        await servicio.RegistrarAsync(new CrearConectorRequest
        {
            Codigo = "CU", Nombre = "CU", Tipo = "REST",
            Credencial = new CrearCredencialRequest
                { Tipo = "Basic", NombreUsuario = "demo", Secreto = "clave123" }
        });

        var dto = await servicio.ActualizarCredencialAsync("CU",
            new CrearCredencialRequest { Tipo = "Basic", NombreUsuario = "otro" });

        var guardado = await repo.GetByCodigoAsync("CU");
        var cred = Assert.Single(guardado!.Credenciales);
        Assert.Equal("otro", cred.NombreUsuario);
        Assert.Equal("clave123", cifrador.Descifrar(cred.ValorCifrado));
        Assert.True(dto.TieneCredencial);
    }

    [Fact]
    public async Task ConnectorService_ValidarWebhook_FirmaCorrectaEIncorrecta()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new WebhookConnector(estado, cifrador, NullLogger<WebhookConnector>.Instance) });
        var conector = new Connector
        {
            IdConnector = 20, Codigo = "WH", Nombre = "WH", Tipo = "Webhook", Activo = true
        };
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "Url", Valor = "https://x" });
        conector.Credenciales.Add(new ConnectorCredential
            { Tipo = "Bearer", ValorCifrado = cifrador.Cifrar("clave-compartida") });
        repo.Agregar(conector);

        const string cuerpo = """{"evento":"x"}""";
        string Firma(string c)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes("clave-compartida"));
            return Convert.ToHexString(hmac.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(c))).ToLowerInvariant();
        }

        var (ok, errorOk) = await servicio.ValidarWebhookAsync("WH", cuerpo, Firma(cuerpo));
        Assert.True(ok, errorOk);
        var (mal, errorMal) = await servicio.ValidarWebhookAsync("WH", cuerpo, "firma-falsa");
        Assert.False(mal);
        Assert.Contains("inválida", errorMal);
        var (sin, _) = await servicio.ValidarWebhookAsync("WH", cuerpo, null);
        Assert.False(sin);
        Assert.Equal(3, exec.Logs.Count);
        Assert.Single(exec.Logs.Where(l => l.Operacion == "Webhook recibido" && l.Estado == "Exitoso"));
        Assert.Equal(2, exec.Logs.Count(l => l.Operacion == "Webhook recibido" && l.Estado == "FirmaInvalida"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servicio.ValidarWebhookAsync("NOPE", cuerpo, "x"));
    }

    [Fact]
    public async Task ConnectorService_Registrar_ValidaTipoYCifraSecreto()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance) });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.RegistrarAsync(new CrearConectorRequest
                { Codigo = "X", Nombre = "X", Tipo = "INVENTADO" }));

        var dto = await servicio.RegistrarAsync(new CrearConectorRequest
        {
            Codigo = "CRM",
            Nombre = "CRM",
            Tipo = "REST",
            Configuracion = new Dictionary<string, string> { ["BaseUrl"] = "https://x" },
            Credencial = new CrearCredencialRequest
                { Tipo = "ApiKey", Secreto = "clave-real" }
        });

        Assert.True(dto.TieneCredencial);
        var guardado = await repo.GetByCodigoAsync("CRM");
        var cred = Assert.Single(guardado!.Credenciales);
        Assert.NotEqual("clave-real", cred.ValorCifrado);
        Assert.Equal("clave-real", cifrador.Descifrar(cred.ValorCifrado));
    }

    [Fact]
    public async Task ConnectorService_Dto_NuncaExponeElSecreto()
    {
        var repo = new RepoMemoria();
        var exec = new ExecMemoria();
        var cifrador = CifradorPrueba();
        var estado = new ConnectorPolicyState();
        var servicio = new ConnectorService(repo, exec, cifrador,
            new IConnector[] { new RestConnector(estado, cifrador, NullLogger<RestConnector>.Instance) });

        var dto = await servicio.RegistrarAsync(new CrearConectorRequest
        {
            Codigo = "CRM2", Nombre = "CRM2", Tipo = "REST",
            Credencial = new CrearCredencialRequest { Tipo = "Bearer", Secreto = "tok-secreto" }
        });

        var serializado = System.Text.Json.JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("tok-secreto", serializado);
        Assert.True(dto.TieneCredencial);
    }

    [Fact]
    public async Task GatewayConnectorTool_ExigeCodigoDeConector()
    {
        var gw = new Mock<IConnectorGateway>();
        var tool = new GatewayConnectorTool(gw.Object, new Mock<IConnectorRepository>().Object);

        var sinCodigo = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "GatewayConnectorTool",
            Parametros = new Dictionary<string, object?>(),
            IdUsuario = 1
        });

        Assert.False(sinCodigo.Exitoso);
        Assert.Contains("conector", sinCodigo.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gateway_Error_ConservaCodigoYResumenEnAuditoria()
    {
        // Antes el fallo auditaba codigo y resumen en null: imposible diagnosticar.
        var repo = new RepoMemoria();
        repo.Agregar(ConectorRest(ajustar: c => c.Politicas.First().MaxReintentos = 0));
        var exec = new ExecMemoria();
        var fake = new FakeHttpHandler();
        fake.Encolar(HttpStatusCode.InternalServerError, "detalle-del-fallo");
        var gw = Gateway(repo, exec, handler: fake);

        var r = await gw.EjecutarAsync(new ConnectorRequest
            { CodigoConector = "CRM_DEMO", IdUsuario = 1 });

        Assert.False(r.Exitoso);
        var log = Assert.Single(exec.Logs);
        Assert.Equal("Error", log.Estado);
        Assert.Equal(500, log.CodigoRespuesta);
        Assert.Contains("detalle-del-fallo", log.RespuestaResumen ?? string.Empty);
    }

    [Fact]
    public async Task GatewayConnectorTool_Etiqueta_NoPisaElDefecto()
    {
        // La etiqueta del planner (objetivo en texto libre) es solo auditoría:
        // el OperacionDefecto manda para el protocolo (SOAPAction, #7021).
        var repo = new Mock<IConnectorRepository>();
        var conector = ConectorRest("CALC");
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "OperacionDefecto", Valor = "Add" });
        repo.Setup(s => s.GetByCodigoAsync("CALC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conector);
        var gw = new Mock<IConnectorGateway>();
        gw.Setup(g => g.EjecutarAsync(It.IsAny<ConnectorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectorResult { Exitoso = true, Contenido = "ok" });
        var tool = new GatewayConnectorTool(gw.Object, repo.Object);

        await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "GatewayConnectorTool",
            Parametros = new Dictionary<string, object?>
            {
                ["conector"] = "CALC",
                ["etiqueta"] = "suma con el conector CALC_SOAP"
            },
            IdUsuario = 1
        });

        gw.Verify(g => g.EjecutarAsync(
            It.Is<ConnectorRequest>(q => q.Operacion == "Add"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GatewayConnectorTool_UsaDefectosDelConector_SinInventarRutas()
    {
        // El planner solo manda {conector, operacion}: el recurso/método salen de
        // los defectos declarados por el admin (RecursoDefecto/MetodoDefecto).
        var repo = new Mock<IConnectorRepository>();
        var conector = ConectorRest("API_DEMO");
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "RecursoDefecto", Valor = "api/health" });
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "MetodoDefecto", Valor = "GET" });
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "CuerpoDefecto", Valor = """{"ping":true}""" });
        repo.Setup(r => r.GetByCodigoAsync("API_DEMO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conector);
        var gw = new Mock<IConnectorGateway>();
        gw.Setup(g => g.EjecutarAsync(It.IsAny<ConnectorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectorResult { Exitoso = true, Contenido = "ok" });
        var tool = new GatewayConnectorTool(gw.Object, repo.Object);

        var r = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "GatewayConnectorTool",
            Parametros = new Dictionary<string, object?> { ["conector"] = "API_DEMO" },
            IdUsuario = 1
        });

        Assert.True(r.Exitoso);
        gw.Verify(g => g.EjecutarAsync(
            It.Is<ConnectorRequest>(q => q.Recurso == "api/health" && q.Metodo == "GET"
                && q.Cuerpo == """{"ping":true}"""),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GatewayConnectorTool_RecursoPorVerbo_MandaSobreElGenerico()
    {
        var repo = new Mock<IConnectorRepository>();
        var conector = ConectorRest("H");
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "RecursoDefecto", Valor = "get" });
        conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "RecursoDefecto_POST", Valor = "post" });
        repo.Setup(r => r.GetByCodigoAsync("H", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conector);
        var gw = new Mock<IConnectorGateway>();
        gw.Setup(g => g.EjecutarAsync(It.IsAny<ConnectorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectorResult { Exitoso = true, Contenido = "ok" });
        var tool = new GatewayConnectorTool(gw.Object, repo.Object);

        await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "GatewayConnectorTool",
            Parametros = new Dictionary<string, object?>
                { ["conector"] = "H", ["metodo"] = "POST" },
            IdUsuario = 1
        });

        gw.Verify(g => g.EjecutarAsync(
            It.Is<ConnectorRequest>(q => q.Recurso == "post" && q.Metodo == "POST"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GatewayConnectorTool_DelegaEnElGateway()
    {
        var gw = new Mock<IConnectorGateway>();
        gw.Setup(g => g.EjecutarAsync(It.IsAny<ConnectorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectorResult
                { Exitoso = true, CodigoRespuesta = 200, Contenido = "ok", LatenciaMs = 5 });
        var tool = new GatewayConnectorTool(gw.Object, new Mock<IConnectorRepository>().Object);

        var r = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "GatewayConnectorTool",
            Parametros = new Dictionary<string, object?>
            {
                ["conector"] = "CRM_DEMO",
                ["recurso"] = "clientes/1",
                ["metodo"] = "GET"
            },
            IdUsuario = 3
        });

        Assert.True(r.Exitoso);
        Assert.Equal("ok", r.Contenido);
        gw.Verify(g => g.EjecutarAsync(
            It.Is<ConnectorRequest>(q => q.CodigoConector == "CRM_DEMO" && q.IdUsuario == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
