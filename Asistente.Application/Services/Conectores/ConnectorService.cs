using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Conectores;

public class ConnectorService
{
    private readonly IConnectorRepository _repo;
    private readonly IConnectorExecutionRepository _execRepo;
    private readonly ICredencialCifrador _cifrador;
    private readonly IEnumerable<IConnector> _conectores;

    public ConnectorService(
        IConnectorRepository repo,
        IConnectorExecutionRepository execRepo,
        ICredencialCifrador cifrador,
        IEnumerable<IConnector> conectores)
    {
        _repo = repo;
        _execRepo = execRepo;
        _cifrador = cifrador;
        _conectores = conectores;
    }

    public async Task<List<ConectorDto>> ListarAsync(CancellationToken ct = default)
    {
        var todos = await _repo.GetAllAsync(ct);
        return todos.Select(ADto).ToList();
    }

    public async Task<ConectorDto?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var c = await _repo.GetByCodigoAsync(codigo, ct);
        return c == null ? null : ADto(c);
    }

    public async Task<ConectorDto> RegistrarAsync(CrearConectorRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            throw new ArgumentException("El código del conector es obligatorio.");
        if (await _repo.GetByCodigoAsync(request.Codigo.Trim(), ct) != null)
            throw new InvalidOperationException($"Ya existe el conector '{request.Codigo}'.");

        var tiposValidos = _conectores.Select(c => c.Tipo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!tiposValidos.Contains(request.Tipo.Trim()))
            throw new InvalidOperationException(
                $"Tipo '{request.Tipo}' no soportado. Tipos: {string.Join(", ", tiposValidos)}.");

        var conector = new Connector
        {
            Codigo = request.Codigo.Trim(),
            Nombre = request.Nombre.Trim(),
            Tipo = request.Tipo.Trim(),
            Descripcion = request.Descripcion,
            Activo = true,
            RequierePermiso = request.RequierePermiso,
            FechaRegistro = DateTime.UtcNow
        };
        foreach (var kv in request.Configuracion)
            conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            {
                Clave = kv.Key.Trim(),
                Valor = kv.Value,
                FechaRegistro = DateTime.UtcNow
            });
        if (request.Credencial != null)
            conector.Credenciales.Add(CrearCredencial(request.Credencial));
        var pol = request.Politica ?? new CrearPoliticaRequest();
        conector.Politicas.Add(new ConnectorPolicy
        {
            TimeoutSegundos = pol.TimeoutSegundos,
            MaxReintentos = pol.MaxReintentos,
            IntervaloReintentoMs = pol.IntervaloReintentoMs,
            RateLimitPorMinuto = pol.RateLimitPorMinuto,
            CircuitBreakerUmbralFallos = pol.CircuitBreakerUmbralFallos,
            CircuitBreakerSegundosAbierto = pol.CircuitBreakerSegundosAbierto,
            FechaRegistro = DateTime.UtcNow
        });

        await _repo.AddAsync(conector, ct);
        return ADto(conector);
    }

    public async Task<ConectorDto> ActualizarCredencialAsync(
        string codigo, CrearCredencialRequest request, CancellationToken ct = default)
    {
        var conector = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'.");
        // Secreto vacío = conservar el almacenado (solo cambian tipo/usuario).
        // Antes se ignoraba todo el cambio si no venía secreto nuevo.
        // Usuario vacío = conservar también (el formulario nunca lo muestra;
        // si se pisara con el vacío, cada guardado borraría el usuario real).
        if (string.IsNullOrWhiteSpace(request.Secreto))
        {
            var actual = conector.Credenciales.FirstOrDefault()
                ?? throw new InvalidOperationException("El conector no tiene credencial que conservar.");
            actual.Tipo = request.Tipo.Trim();
            if (!string.IsNullOrWhiteSpace(request.NombreUsuario))
                actual.NombreUsuario = request.NombreUsuario;
        }
        else
        {
            conector.Credenciales.Clear();
            conector.Credenciales.Add(CrearCredencial(request));
        }
        await _repo.UpdateAsync(conector, ct);
        return ADto(await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'."));
    }

    public async Task CambiarEstadoAsync(string codigo, bool activo, CancellationToken ct = default)
    {
        var conector = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'.");
        conector.Activo = activo;
        await _repo.UpdateAsync(conector, ct);
    }

    /// <summary>
    /// Actualiza los datos básicos (nombre, descripción, permiso requerido).
    /// No toca configuración (ActualizarConfiguracionAsync), credenciales
    /// (ActualizarCredencialAsync), tipo ni código: esos identifican al conector.
    /// </summary>
    public async Task<ConectorDto> ActualizarDatosAsync(
        string codigo, string nombre, string? descripcion, bool requierePermiso,
        CancellationToken ct = default)
    {
        var conector = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'.");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.");
        conector.Nombre = nombre.Trim();
        conector.Descripcion = descripcion;
        conector.RequierePermiso = requierePermiso;
        await _repo.UpdateAsync(conector, ct);
        return ADto(await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'."));
    }

    /// <summary>
    /// Actualiza la configuración no secreta por mezcla (upsert por clave): las
    /// claves enviadas con valor se crean o reemplazan; las enviadas VACÍAS se
    /// ELIMINAN (así la UI puede quitar claves sin SQL); las omitidas quedan
    /// intactas. No toca credenciales (van por ActualizarCredencialAsync).
    /// Todo cambio rige para próximas ejecuciones.
    /// </summary>
    public async Task<ConectorDto> ActualizarConfiguracionAsync(
        string codigo, Dictionary<string, string> configuracion, CancellationToken ct = default)
    {
        var conector = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'.");
        foreach (var kv in configuracion)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            var clave = kv.Key.Trim();
            var existente = conector.Configuraciones.FirstOrDefault(c =>
                c.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(kv.Value))
            {
                if (existente != null)
                    conector.Configuraciones.Remove(existente);
                continue;
            }
            if (existente != null)
                existente.Valor = kv.Value;
            else
                conector.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
                {
                    Clave = clave,
                    Valor = kv.Value,
                    FechaRegistro = DateTime.UtcNow
                });
        }
        await _repo.UpdateAsync(conector, ct);
        return ADto(await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'."));
    }

    public async Task<(bool Ok, string? Error)> ProbarConexionAsync(string codigo, CancellationToken ct = default)
    {
        var conector = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'.");
        var impl = _conectores.FirstOrDefault(c =>
            c.Tipo.Equals(conector.Tipo, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No hay implementación para el tipo '{conector.Tipo}'.");
        var inicio = DateTime.UtcNow;
        var (ok, error) = await impl.ProbarConexionAsync(conector, ct);

        // El diagnóstico también se audita (evento ProbarConexion): "toda
        // ejecución queda auditada". No consume presupuesto de RateLimit ni
        // toca el CircuitBreaker: es lectura de salud, no llamada de datos.
        try
        {
            await _execRepo.AddAsync(new ConnectorExecution
            {
                IdConnector = conector.IdConnector,
                Operacion = "ProbarConexion",
                Destino = conector.Tipo,
                Estado = ok ? "Exitoso" : "Error",
                LatenciaMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds,
                Error = error,
                Fecha = DateTime.UtcNow
            }, ct);
        }
        catch { /* la auditoría nunca tumba el diagnóstico */ }
        return (ok, error);
    }

    public Task<ConnectorMetricas> MetricasAsync(int idConnector, CancellationToken ct = default)
        => _execRepo.GetMetricasAsync(idConnector, ct);

    /// <summary>
    /// Receptor entrante de webhooks (ETAPA 20): valida la firma HMAC del cuerpo
    /// contra el secreto del conector y audita el resultado (Recibido o
    /// FirmaInvalida). Comparación en tiempo constante para no filtrar por timing.
    /// Cierra el círculo: nuestros propios envíos se pueden validar aquí.
    /// </summary>
    public async Task<(bool Valido, string? Error)> ValidarWebhookAsync(
        string codigo, string cuerpo, string? firma, CancellationToken ct = default)
    {
        var conector = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'.");
        if (!conector.Tipo.Equals("Webhook", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"El conector '{codigo}' no es de tipo Webhook.");
        if (!conector.Activo)
            throw new InvalidOperationException($"El conector '{codigo}' está deshabilitado.");

        string? error = null;
        var cred = conector.Credenciales.FirstOrDefault();
        if (cred == null || string.IsNullOrWhiteSpace(firma))
        {
            error = cred == null
                ? "El conector no tiene secreto configurado para validar."
                : "Falta la cabecera de firma.";
        }
        else
        {
            var esperado = FirmarHmacLocal(_cifrador.Descifrar(cred.ValorCifrado), cuerpo);
            var a = System.Text.Encoding.UTF8.GetBytes(esperado);
            var b = System.Text.Encoding.UTF8.GetBytes(firma.Trim().ToLowerInvariant());
            if (a.Length != b.Length
                || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b))
                error = "Firma inválida.";
        }

        try
        {
            await _execRepo.AddAsync(new ConnectorExecution
            {
                IdConnector = conector.IdConnector,
                Operacion = "Webhook recibido",
                Destino = "inbound",
                Estado = error == null ? "Exitoso" : "FirmaInvalida",
                LatenciaMs = 0,
                Error = error,
                RespuestaResumen = cuerpo.Length > 500 ? cuerpo[..500] : cuerpo,
                Fecha = DateTime.UtcNow
            }, ct);
        }
        catch { /* la auditoría nunca tumba la recepción */ }
        return (error == null, error);
    }

    private static string FirmarHmacLocal(string secreto, string contenido)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secreto));
        return Convert.ToHexString(hmac.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(contenido))).ToLowerInvariant();
    }

    public Task<List<ConnectorExecution>> AuditoriaAsync(int idConnector, int tope = 100, CancellationToken ct = default)
        => _execRepo.GetByConnectorAsync(idConnector, tope, ct);

    private ConnectorCredential CrearCredencial(CrearCredencialRequest request)
    {
        string? parametrosJson = null;
        if (request.Parametros.Count > 0)
            parametrosJson = _cifrador.Cifrar(
                System.Text.Json.JsonSerializer.Serialize(request.Parametros));
        return new ConnectorCredential
        {
            Tipo = request.Tipo.Trim(),
            NombreUsuario = request.NombreUsuario,
            ValorCifrado = _cifrador.Cifrar(request.Secreto ?? string.Empty),
            ParametrosCifrados = parametrosJson,
            FechaRegistro = DateTime.UtcNow
        };
    }

    private static ConectorDto ADto(Connector c)
    {
        var cred = c.Credenciales.FirstOrDefault();
        var pol = c.Politicas.FirstOrDefault();
        return new ConectorDto
        {
            IdConnector = c.IdConnector,
            Codigo = c.Codigo,
            Nombre = c.Nombre,
            Tipo = c.Tipo,
            Descripcion = c.Descripcion,
            Activo = c.Activo,
            RequierePermiso = c.RequierePermiso,
            Configuracion = c.Configuraciones.ToDictionary(x => x.Clave, x => x.Valor),
            TipoCredencial = cred?.Tipo,
            TieneCredencial = cred != null && !string.IsNullOrEmpty(cred.ValorCifrado),
            Politica = pol == null ? null : new CrearPoliticaRequest
            {
                TimeoutSegundos = pol.TimeoutSegundos,
                MaxReintentos = pol.MaxReintentos,
                IntervaloReintentoMs = pol.IntervaloReintentoMs,
                RateLimitPorMinuto = pol.RateLimitPorMinuto,
                CircuitBreakerUmbralFallos = pol.CircuitBreakerUmbralFallos,
                CircuitBreakerSegundosAbierto = pol.CircuitBreakerSegundosAbierto
            }
        };
    }
}
