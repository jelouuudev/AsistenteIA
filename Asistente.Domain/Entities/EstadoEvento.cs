namespace Asistente.Domain.Entities;

/// <summary>
/// Estados posibles de un evento procesado o de una tarea programada
/// (Requerimiento Funcional ETAPA 13 - Actividad 5/9).
/// </summary>
public enum EstadoEvento
{
    Pendiente = 0,
    EnProceso = 1,
    Completado = 2,
    Error = 3,
    Reintentando = 4
}
