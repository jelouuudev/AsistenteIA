using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IWorkflowService
{
    Task<IEnumerable<WorkflowDto>> ObtenerTodosAsync(CancellationToken ct = default);
    Task<WorkflowDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<WorkflowEjecucionDto>> ObtenerEjecucionesAsync(CancellationToken ct = default);
    Task<WorkflowDto> CrearAsync(CrearWorkflowRequest request, CancellationToken ct = default);
    Task ActualizarAsync(int id, ActualizarWorkflowRequest request, CancellationToken ct = default);
    Task CambiarEstadoAsync(int id, EstadoWorkflow estado, CancellationToken ct = default);
    Task VersionarAsync(int id, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
    Task<WorkflowExecutionResult> EjecutarWorkflowAsync(int id, int idUsuario, Dictionary<string, object>? parametros = null, CancellationToken ct = default);
}

public interface IConfiguracionWorkflowService
{
    Task<ConfiguracionWorkflowDto> ObtenerAsync(CancellationToken ct = default);
    Task GuardarAsync(ConfiguracionWorkflowDto config, CancellationToken ct = default);
}
