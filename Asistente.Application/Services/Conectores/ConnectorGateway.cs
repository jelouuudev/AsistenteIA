using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Seguridad;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Conectores;

/// <summary>
/// API Gateway Empresarial (ETAPA 20): única puerta de salida hacia sistemas
/// externos. Resuelve el conector por código, verifica que esté habilitado y que
/// el usuario tenga permiso, aplica Timeout/Retry/RateLimit/CircuitBreaker y
/// audita cada ejecución. Los agentes nunca llaman conectores directamente.
/// </summary>
public class ConnectorGateway : IConnectorGateway
{
    private readonly IConnectorRepository _repo;
    private readonly IConnectorExecutionRepository _execRepo;
    private readonly IEnumerable<IConnector> _conectores;
    private readonly ConnectorPolicyState _estado;
    private readonly IAutorizacionService _autorizacion;
    private readonly ILogger<ConnectorGateway> _logger;

    public ConnectorGateway(
        IConnectorRepository repo,
        IConnectorExecutionRepository execRepo,
        IEnumerable<IConnector> conectores,
        ConnectorPolicyState estado,
        IAutorizacionService autorizacion,
        ILogger<ConnectorGateway> logger)
    {
        _repo = repo;
        _execRepo = execRepo;
        _conectores = conectores;
        _estado = estado;
        _autorizacion = autorizacion;
        _logger = logger;
    }

    public async Task<ConnectorResult> EjecutarAsync(ConnectorRequest request, CancellationToken ct = default)
    {
        var inicio = Stopwatch.StartNew();
        var reintentos = 0;

        var conector = await _repo.GetByCodigoAsync(request.CodigoConector, ct);
        if (conector == null)
            return await AuditarAsync(null, request, "ConectorNoExiste", null,
                $"No existe el conector '{request.CodigoConector}'.", 0, inicio, ct);

        if (!conector.Activo)
            return await AuditarAsync(conector, request, "Deshabilitado", null,
                $"El conector '{conector.Codigo}' está deshabilitado.", 0, inicio, ct);

        if (conector.RequierePermiso)
        {
            var permiso = await _autorizacion.VerificarPermisoAsync(
                request.IdUsuario, $"CONNECTOR:{conector.Codigo}", ct);
            if (!permiso.Permitido)
                return await AuditarAsync(conector, request, "SinPermiso", null,
                    $"El usuario no tiene permiso CONNECTOR:{conector.Codigo}.", 0, inicio, ct);
        }

        var impl = _conectores.FirstOrDefault(c =>
            c.Tipo.Equals(conector.Tipo, StringComparison.OrdinalIgnoreCase));
        if (impl == null)
            return await AuditarAsync(conector, request, "SinImplementacion", null,
                $"No hay implementación para el tipo '{conector.Tipo}'.", 0, inicio, ct);

        var politica = conector.Politicas.FirstOrDefault() ?? new ConnectorPolicy();

        if (_estado.CircuitoAbierto(conector.IdConnector))
            return await AuditarAsync(conector, request, "CircuitoAbierto", null,
                "Circuito abierto por fallos consecutivos; rechazo rápido.", 0, inicio, ct);

        if (!_estado.PermitePorRateLimit(conector.IdConnector, politica.RateLimitPorMinuto))
            return await AuditarAsync(conector, request, "RateLimit", null,
                $"Límite de {politica.RateLimitPorMinuto} llamadas/minuto excedido.", 0, inicio, ct);

        var maxIntentos = Math.Max(politica.MaxReintentos, 0) + 1;
        Exception? ultimoError = null;

        for (var intento = 1; intento <= maxIntentos; intento++)
        {
            using var ctsTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ctsTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(politica.TimeoutSegundos, 1)));
            try
            {
                var resultado = await impl.EjecutarAsync(conector, request, ctsTimeout.Token);
                resultado.Reintentos = reintentos;
                resultado.LatenciaMs = inicio.ElapsedMilliseconds;

                if (resultado.Exitoso)
                {
                    _estado.RegistrarExito(conector.IdConnector);
                    await AuditarAsync(conector, request, "Exitoso", resultado.CodigoRespuesta,
                        null, reintentos, inicio, CancellationToken.None, resultado);
                    return resultado;
                }

                ultimoError = new InvalidOperationException(resultado.Error ?? "Error del conector.");
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                ultimoError = new TimeoutException(
                    $"El conector '{conector.Codigo}' excedió el timeout de {politica.TimeoutSegundos}s.", ex);
            }
            catch (Exception ex)
            {
                ultimoError = ex;
            }

            if (intento < maxIntentos)
            {
                reintentos++;
                _logger.LogWarning("Gateway: intento {Intento}/{Max} del conector '{Codigo}' falló; reintentando. {Error}",
                    intento, maxIntentos, conector.Codigo, ultimoError?.Message);
                try { await Task.Delay(politica.IntervaloReintentoMs, ct); } catch (OperationCanceledException) { break; }
            }
        }

        _estado.RegistrarFallo(conector.IdConnector,
            politica.CircuitBreakerUmbralFallos, politica.CircuitBreakerSegundosAbierto);
        return await AuditarAsync(conector, request, "Error", null,
            ultimoError?.Message ?? "Error desconocido.", reintentos, inicio, CancellationToken.None);
    }

    private async Task<ConnectorResult> AuditarAsync(
        Connector? conector, ConnectorRequest request, string estado, int? codigo,
        string? error, int reintentos, Stopwatch inicio, CancellationToken ct,
        ConnectorResult? resultado = null)
    {
        var latencia = inicio.ElapsedMilliseconds;
        try
        {
            if (conector != null)
            {
                await _execRepo.AddAsync(new ConnectorExecution
                {
                    IdConnector = conector.IdConnector,
                    IdUsuario = request.IdUsuario == 0 ? null : request.IdUsuario,
                    IdAsistente = request.IdAsistente,
                    Operacion = string.IsNullOrWhiteSpace(request.Operacion)
                        ? $"{request.Metodo} {request.Recurso}".Trim()
                        : request.Operacion,
                    Destino = Recortar(SanitizarDestino(request), 1000),
                    Estado = estado,
                    CodigoRespuesta = codigo,
                    LatenciaMs = latencia,
                    Reintentos = reintentos,
                    Error = Recortar(error, 2000),
                    RespuestaResumen = Recortar(resultado?.Contenido, 2000),
                    Fecha = DateTime.UtcNow
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gateway: no se pudo auditar la ejecución del conector '{Codigo}'.", request.CodigoConector);
        }

        if (resultado != null) return resultado;
        return new ConnectorResult
        {
            Exitoso = false,
            CodigoRespuesta = codigo,
            Error = error,
            LatenciaMs = latencia,
            Reintentos = reintentos
        };
    }

    /// <summary>Destino sin secretos: nunca se auditan query strings (pueden traer tokens).</summary>
    private static string SanitizarDestino(ConnectorRequest request)
    {
        var recurso = (request.Recurso ?? string.Empty).Split('?')[0];
        return $"{request.Metodo} {recurso}".Trim();
    }

    private static string? Recortar(string? texto, int max)
        => string.IsNullOrEmpty(texto) ? texto
            : texto.Length <= max ? texto : texto[..max];
}
