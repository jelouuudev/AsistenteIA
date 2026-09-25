namespace Asistente.Domain.Entities;

/// <summary>
/// Estados del ciclo de vida de un agente.
/// Activo: el agente está disponible para usar.
/// Inactivo: el agente está deshabilitado.
/// </summary>
public enum EstadoAgente
{
    Activo = 0,
    Inactivo = 1
}
