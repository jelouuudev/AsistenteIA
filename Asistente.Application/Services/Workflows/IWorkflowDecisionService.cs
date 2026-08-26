using System.Threading;
using System.Threading.Tasks;

namespace Asistente.Application.Services.Workflows;

/// <summary>
/// Identifica el flujo de trabajo que corresponde a una solicitud del usuario.
/// </summary>
public interface IWorkflowDecisionService
{
    Task<WorkflowDecision> DecidirAsync(string mensaje, CancellationToken cancellationToken = default);
}
