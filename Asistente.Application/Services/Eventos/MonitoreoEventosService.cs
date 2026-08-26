using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Eventos;

/// <summary>
/// Panel de monitoreo del Motor de Eventos (ETAPA 13 - Actividad 8/9).
/// Consolida eventos, reglas, tareas y ejecuciones para visualización y auditoría.
/// </summary>
public class MonitoreoEventosService : IMonitoreoEventosService
{
    private readonly IEventoEmpresarialRepository _eventoRepository;
    private readonly IReglaEventoRepository _reglaRepository;
    private readonly IEventoProcesadoRepository _eventoProcesadoRepository;
    private readonly ITareaProgramadaRepository _tareaRepository;

    public MonitoreoEventosService(
        IEventoEmpresarialRepository eventoRepository,
        IReglaEventoRepository reglaRepository,
        IEventoProcesadoRepository eventoProcesadoRepository,
        ITareaProgramadaRepository tareaRepository)
    {
        _eventoRepository = eventoRepository;
        _reglaRepository = reglaRepository;
        _eventoProcesadoRepository = eventoProcesadoRepository;
        _tareaRepository = tareaRepository;
    }

    public async Task<MonitoreoEventosDto> ObtenerPanelAsync(CancellationToken ct = default)
    {
        var eventos = await _eventoRepository.GetAllAsync(ct);
        var reglas = await _reglaRepository.GetAllAsync(ct);
        var eventosProcesados = await _eventoProcesadoRepository.GetAllAsync(ct);
        var tareas = await _tareaRepository.GetAllAsync(ct);

        return new MonitoreoEventosDto
        {
            TotalEventos = eventos.Count(),
            EventosActivos = eventos.Count(e => e.Activo),
            TotalReglas = reglas.Count(),
            ReglasActivas = reglas.Count(r => r.Activa),
            TotalEventosProcesados = eventosProcesados.Count(),
            EventosExitosos = eventosProcesados.Count(e => e.Estado == "Completado"),
            EventosConError = eventosProcesados.Count(e => e.Estado == "Error"),
            EventosReintentando = eventosProcesados.Count(e => e.Estado is "Reintentando" or "EnProceso"),
            TotalTareas = tareas.Count(),
            TareasActivas = tareas.Count(t => t.Activa),
            UltimosEventosProcesados = eventosProcesados.Take(20).Select(MapEvento).ToList(),
            ReglasActivasDetalle = reglas.Where(r => r.Activa).Select(MapRegla).ToList(),
            TareasDetalle = tareas.Select(MapTarea).ToList()
        };
    }

    private static EventoProcesadoDto MapEvento(Asistente.Domain.Entities.EventoProcesado e) => new()
    {
        IdEventoProcesado = e.IdEventoProcesado,
        IdEvento = e.IdEvento,
        CodigoEvento = e.Evento?.Codigo ?? string.Empty,
        NombreEvento = e.Evento?.Nombre ?? string.Empty,
        FechaHora = e.FechaHora,
        Estado = e.Estado,
        Resultado = e.Resultado,
        TiempoProcesamiento = e.TiempoProcesamiento,
        IdRegla = e.IdRegla,
        IdWorkflow = e.IdWorkflow
    };

    private static ReglaEventoDto MapRegla(Asistente.Domain.Entities.ReglaEvento r) => new()
    {
        IdRegla = r.IdRegla,
        IdEvento = r.IdEvento,
        NombreEvento = r.Evento?.Nombre ?? string.Empty,
        IdWorkflow = r.IdWorkflow,
        NombreWorkflow = r.Workflow?.Nombre ?? string.Empty,
        Condicion = r.Condicion,
        Prioridad = r.Prioridad,
        Activa = r.Activa
    };

    private static TareaProgramadaDto MapTarea(Asistente.Domain.Entities.TareaProgramada t) => new()
    {
        IdTarea = t.IdTarea,
        Nombre = t.Nombre,
        ExpresionCron = t.ExpresionCron,
        IdWorkflow = t.IdWorkflow,
        NombreWorkflow = t.Workflow?.Nombre ?? string.Empty,
        Activa = t.Activa,
        UltimaEjecucion = t.UltimaEjecucion,
        ProximaEjecucion = t.ProximaEjecucion
    };
}
