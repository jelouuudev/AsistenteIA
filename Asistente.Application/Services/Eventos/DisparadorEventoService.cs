using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Asistente.Application.Services.Eventos;

public class DisparadorEventoService : IDisparadorEventoService
{
    private readonly IDisparadorEventoRepository _repository;
    private readonly IEventoEmpresarialRepository _eventoRepository;
    private readonly IConexionBaseDatosRepository _conexionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DisparadorEventoService> _logger;

    private static readonly string[] TiposValidos =
        { DisparadorEvento.Tipos.Cron, DisparadorEvento.Tipos.SondeoBD, DisparadorEvento.Tipos.Documento };

    public DisparadorEventoService(
        IDisparadorEventoRepository repository,
        IEventoEmpresarialRepository eventoRepository,
        IConexionBaseDatosRepository conexionRepository,
        IUnitOfWork unitOfWork,
        ILogger<DisparadorEventoService> logger)
    {
        _repository = repository;
        _eventoRepository = eventoRepository;
        _conexionRepository = conexionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<DisparadorEventoDto>> ObtenerTodosAsync(CancellationToken ct = default)
    {
        var lista = await _repository.GetAllAsync(ct);
        return lista.Select(d => Map(d, d.Evento));
    }

    public async Task<DisparadorEventoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var d = await _repository.GetByIdAsync(id, ct);
        return d == null ? null : Map(d, d.Evento);
    }

    public async Task<IEnumerable<DisparadorEventoDto>> ObtenerActivosAsync(CancellationToken ct = default)
    {
        var lista = await _repository.GetActivosAsync(ct);
        return lista.Select(d => Map(d, d.Evento));
    }

    public async Task<DisparadorEventoDto> CrearAsync(CrearDisparadorEventoRequest request, CancellationToken ct = default)
    {
        var tipo = NormalizarTipo(request.Tipo);
        var config = string.IsNullOrWhiteSpace(request.ConfigJson) ? "{}" : request.ConfigJson.Trim();
        await ValidarConfigAsync(tipo, config, ct);

        var evento = await _eventoRepository.GetByIdAsync(request.IdEvento, ct)
            ?? throw new KeyNotFoundException("El evento origen no existe.");

        var disparador = new DisparadorEvento
        {
            IdEvento = request.IdEvento,
            Tipo = tipo,
            Activo = request.Activo,
            ConfigJson = config,
            FechaCreacion = DateTime.UtcNow
        };
        await _repository.AddAsync(disparador, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Disparador {Tipo} creado para evento {Evento}.", tipo, evento.Codigo);
        return Map(disparador, evento);
    }

    public async Task ActualizarAsync(int id, ActualizarDisparadorEventoRequest request, CancellationToken ct = default)
    {
        var disparador = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Disparador no encontrado.");
        var tipo = NormalizarTipo(request.Tipo);
        var config = string.IsNullOrWhiteSpace(request.ConfigJson) ? "{}" : request.ConfigJson.Trim();
        await ValidarConfigAsync(tipo, config, ct);

        disparador.Tipo = tipo;
        disparador.ConfigJson = config;
        disparador.Activo = request.Activo;
        await _repository.UpdateAsync(disparador, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default)
    {
        var disparador = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Disparador no encontrado.");
        disparador.Activo = activo;
        await _repository.UpdateAsync(disparador, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var disparador = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Disparador no encontrado.");
        await _repository.DeleteAsync(disparador, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task MarcarEjecucionAsync(int id, DateTime? proxima, CancellationToken ct = default)
    {
        var disparador = await _repository.GetByIdAsync(id, ct);
        if (disparador == null) return;
        disparador.UltimaEjecucion = DateTime.UtcNow;
        disparador.ProximaEjecucion = proxima;
        await _repository.UpdateAsync(disparador, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<DisparadorEventoDto>> ObtenerDisparadoresDocumentoAsync(int? idCategoria, string? codigoDocumento, CancellationToken ct = default)
    {
        var activos = await _repository.GetActivosAsync(ct);
        var resultado = new List<DisparadorEventoDto>();
        foreach (var d in activos)
        {
            if (d.Tipo != DisparadorEvento.Tipos.Documento) continue;
            try
            {
                using var doc = JsonDocument.Parse(d.ConfigJson ?? "{}");
                var root = doc.RootElement;
                var cat = root.TryGetProperty("idCategoria", out var c) ? c.GetInt32() : (int?)null;
                var cod = root.TryGetProperty("codigoDocumento", out var cd) ? cd.GetString() : null;
                if ((idCategoria == null || cat == null || cat == idCategoria) &&
                    (codigoDocumento == null || cod == null || cod == codigoDocumento))
                {
                    resultado.Add(Map(d, d.Evento));
                }
            }
            catch { /* ignorar config malformada */ }
        }
        return resultado;
    }

    private static string NormalizarTipo(string tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) throw new ArgumentException("El tipo de disparador es obligatorio.");
        var t = tipo.Trim();
        foreach (var valido in TiposValidos)
        {
            if (string.Equals(t, valido, StringComparison.OrdinalIgnoreCase)) return valido;
        }
        throw new ArgumentException($"Tipo de disparador no válido. Opciones: {string.Join(", ", TiposValidos)}");
    }

    private async Task ValidarConfigAsync(string tipo, string config, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(config);
            var root = doc.RootElement;
            if (tipo == DisparadorEvento.Tipos.Cron)
            {
                if (!root.TryGetProperty("cron", out var cron) || cron.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(cron.GetString()))
                    throw new ArgumentException("ConfigJson para Cron debe incluir la propiedad 'cron' (expresión cron).");
            }
            else if (tipo == DisparadorEvento.Tipos.SondeoBD)
            {
                if (!root.TryGetProperty("idConexion", out var idCon) || idCon.ValueKind != JsonValueKind.Number)
                    throw new ArgumentException("ConfigJson para SondeoBD debe incluir 'idConexion' (número).");
                if (!root.TryGetProperty("consultaSql", out var sql) || sql.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(sql.GetString()))
                    throw new ArgumentException("ConfigJson para SondeoBD debe incluir 'consultaSql' (SELECT).");
                var idC = idCon.GetInt32();
                var conexion = await _conexionRepository.GetByIdAsync(idC);
                if (conexion == null) throw new KeyNotFoundException($"La conexión BD {idC} no existe.");
            }
            else if (tipo == DisparadorEvento.Tipos.Documento)
            {
                // Opcional: idCategoria y/o codigoDocumento
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"ConfigJson no es un JSON válido: {ex.Message}");
        }
    }

    private static DisparadorEventoDto Map(DisparadorEvento d, EventoEmpresarial? e = null) => new()
    {
        IdDisparador = d.IdDisparador,
        IdEvento = d.IdEvento,
        NombreEvento = e?.Nombre ?? string.Empty,
        CodigoEvento = e?.Codigo ?? string.Empty,
        Tipo = d.Tipo,
        Activo = d.Activo,
        ConfigJson = d.ConfigJson,
        UltimaEjecucion = d.UltimaEjecucion,
        ProximaEjecucion = d.ProximaEjecucion
    };
}
