namespace Asistente.Domain.Entities;

/// <summary>
/// Estados del ciclo de vida de un agente (ETAPA 16).
/// Borrador -> Prueba -> Publicado -> (Deshabilitado)
/// </summary>
public enum EstadoAgente
{
    Borrador = 0,
    Prueba = 1,
    Publicado = 2,
    Deshabilitado = 3
}
