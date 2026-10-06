using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Conectores;

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
    /// <summary>Secreto en claro SOLO en tránsito: se cifra antes de persistir.</summary>
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
    /// <summary>Las credenciales NUNCA se devuelven: solo su tipo y si existen.</summary>
    public string? TipoCredencial { get; set; }
    public bool TieneCredencial { get; set; }
    public CrearPoliticaRequest? Politica { get; set; }
}

/// <summary>
/// Administración de conectores (ETAPA 20): registrar, configurar, habilitar y
/// deshabilitar sin recompilar. Los secretos se cifran al guardar y jamás se
/// devuelven en lectura (ni siquiera al administrador).
/// </summary>
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
        conector.Credenciales.Clear();
        conector.Credenciales.Add(CrearCredencial(request));
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

    public async Task<(bool Ok, string? Error)> ProbarConexionAsync(string codigo, CancellationToken ct = default)
    {
        var conector = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe el conector '{codigo}'.");
        var impl = _conectores.FirstOrDefault(c =>
            c.Tipo.Equals(conector.Tipo, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No hay implementación para el tipo '{conector.Tipo}'.");
        return await impl.ProbarConexionAsync(conector, ct);
    }

    public Task<ConnectorMetricas> MetricasAsync(int idConnector, CancellationToken ct = default)
        => _execRepo.GetMetricasAsync(idConnector, ct);

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
