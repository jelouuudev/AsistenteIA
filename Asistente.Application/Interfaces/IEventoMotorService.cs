using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IEventoEmpresarialService
{
    Task<IEnumerable<EventoEmpresarialDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<EventoEmpresarialDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<EventoEmpresarialDto> CrearAsync(CrearEventoEmpresarialRequest request, CancellationToken ct = default);
    Task ActualizarAsync(int id, ActualizarEventoEmpresarialRequest request, CancellationToken ct = default);
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<ReglaEventoDto>> ObtenerReglasAsync(int idEvento, CancellationToken ct = default);
}

public interface IReglaEventoService
{
    Task<IEnumerable<ReglaEventoDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<ReglaEventoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<ReglaEventoDto> CrearAsync(CrearReglaEventoRequest request, CancellationToken ct = default);
    Task ActualizarAsync(int id, ActualizarReglaEventoRequest request, CancellationToken ct = default);
    Task CambiarEstadoAsync(int id, bool activa, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public interface IEventoProcesadoService
{
    Task<IEnumerable<EventoProcesadoDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<IEnumerable<EventoProcesadoDto>> ObtenerPorEventoAsync(int idEvento, CancellationToken ct = default);
}

public interface ITareaProgramadaService
{
    Task<IEnumerable<TareaProgramadaDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<TareaProgramadaDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<TareaProgramadaDto> CrearAsync(CrearTareaProgramadaRequest request, CancellationToken ct = default);
    Task ActualizarAsync(int id, ActualizarTareaProgramadaRequest request, CancellationToken ct = default);
    Task CambiarEstadoAsync(int id, bool activa, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public interface IConfiguracionEventoMotorService
{
    Task<ConfiguracionEventoMotorDto> ObtenerAsync(CancellationToken ct = default);
    Task GuardarAsync(ConfiguracionEventoMotorDto config, CancellationToken ct = default);
}

/// <summary>
/// Motor de Eventos Empresariales: detecta eventos, evalúa reglas y ejecuta workflows automáticamente.
/// </summary>
public interface IEventoMotorService
{
    /// <summary>
    /// Dispara un evento por su código, evaluando las reglas asociadas y ejecutando los workflows
    /// correspondientes de forma automática y desacoplada (Actividad 4).
    /// </summary>
    Task<EventoProcesadoDto> DispararEventoAsync(string codigoEvento, string? contextoJson = null, int? idUsuario = null, CancellationToken ct = default);

    /// <summary>Procesa un evento ya persistido (usado por el procesador en segundo plano con reintentos).</summary>
    Task ProcesarEventoAsync(int idEventoProcesado, CancellationToken ct = default);
}

public interface IMonitoreoEventosService
{
    Task<MonitoreoEventosDto> ObtenerPanelAsync(CancellationToken ct = default);
}

public interface IDisparadorEventoService
{
    Task<IEnumerable<DisparadorEventoDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<DisparadorEventoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<DisparadorEventoDto>> ObtenerActivosAsync(CancellationToken ct = default);
    Task<DisparadorEventoDto> CrearAsync(CrearDisparadorEventoRequest request, CancellationToken ct = default);
    Task ActualizarAsync(int id, ActualizarDisparadorEventoRequest request, CancellationToken ct = default);
    Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
    Task MarcarEjecucionAsync(int id, DateTime? proxima, CancellationToken ct = default);
    Task<IEnumerable<DisparadorEventoDto>> ObtenerDisparadoresDocumentoAsync(int? idCategoria, string? codigoDocumento, CancellationToken ct = default);
}
