using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Asistente.Application.Services.Workflows;

/// <summary>
/// Contrato del Motor de Flujos de Trabajo (Workflow Engine).
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Ejecuta (o reanuda) un flujo de trabajo de forma secuencial.
    /// </summary>
    /// <param name="idWorkflow">Identificador del flujo.</param>
    /// <param name="idUsuario">Usuario que ejecuta el flujo.</param>
    /// <param name="idAsistente">Asistente desde el cual se invoca (opcional).</param>
    /// <param name="confirmado">Indica si el usuario ya confirmó los pasos sensibles.</param>
    /// <param name="idEjecucionExistente">Si se reanuda tras confirmación, la ejecución previa.</param>
    /// <param name="contextoInicial">Datos iniciales disponibles como {{Clave}} en los parámetros
    /// de los pasos (ej. datos del evento que originó la ejecución).</param>
    Task<WorkflowExecutionResult> EjecutarAsync(
        int idWorkflow, int idUsuario, int? idAsistente, bool confirmado = false,
        int? idEjecucionExistente = null, CancellationToken cancellationToken = default,
        Dictionary<string, string>? contextoInicial = null);

    /// <summary>
    /// Reanuda la ejecución pendiente de confirmación de un usuario (si existe) tras que este confirma.
    /// </summary>
    Task<WorkflowExecutionResult?> ReanudarPendienteConfirmacionAsync(
        int idUsuario, int? idAsistente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un flujo activo cuya frase disparadora aparezca en el mensaje (matching
    /// determinista, sin LLM). Gana la frase coincidente más larga (más específica).
    /// </summary>
    Task<Domain.Entities.Workflow?> BuscarPorDisparadorAsync(
        string mensaje, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela la ejecución pendiente de confirmación de un usuario (si existe).
    /// </summary>
    Task<WorkflowExecutionResult?> CancelarPendienteConfirmacionAsync(
        int idUsuario, CancellationToken cancellationToken = default);
}
