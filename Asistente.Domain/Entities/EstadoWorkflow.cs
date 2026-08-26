namespace Asistente.Domain.Entities;

/// <summary>
/// Estados sugeridos por el Requerimiento Funcional para un flujo de trabajo.
/// </summary>
public enum EstadoWorkflow
{
    Borrador = 0,
    Activo = 1,
    Suspendido = 2,
    Finalizado = 3,
    Error = 4
}
