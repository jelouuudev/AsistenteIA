using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Eventos;

public class EventoProcesadoService : IEventoProcesadoService
{
    private readonly IEventoProcesadoRepository _repository;

    public EventoProcesadoService(IEventoProcesadoRepository repository) => _repository = repository;

    public async Task<IEnumerable<EventoProcesadoDto>> ObtenerTodosAsync(CancellationToken ct = default)
        => (await _repository.GetAllAsync(ct)).Select(Map);

    public async Task<IEnumerable<EventoProcesadoDto>> ObtenerPorEventoAsync(int idEvento, CancellationToken ct = default)
        => (await _repository.GetByEventoAsync(idEvento, ct)).Select(Map);

    private static EventoProcesadoDto Map(EventoProcesado e) => new()
    {
        IdEventoProcesado = e.IdEventoProcesado,
        IdEvento = e.IdEvento,
        CodigoEvento = e.Evento?.Codigo ?? string.Empty,
        NombreEvento = e.Evento?.Nombre ?? string.Empty,
        FechaHora = e.FechaHora,
        Estado = e.Estado,
        Resultado = e.Resultado,
        ContextoDisparo = e.ContextoDisparo,
        TiempoProcesamiento = e.TiempoProcesamiento,
        IdRegla = e.IdRegla,
        IdWorkflow = e.IdWorkflow,
        NombreWorkflow = e.Workflow?.Nombre ?? string.Empty
    };
}
