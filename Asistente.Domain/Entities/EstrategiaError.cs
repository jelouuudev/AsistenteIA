namespace Asistente.Domain.Entities;

/// <summary>
/// Estrategia aplicada cuando un paso de un flujo de trabajo falla tras agotar los reintentos.
/// (Requerimiento Funcional - Actividad 5: Manejo de errores).
/// </summary>
public enum EstrategiaError
{
    /// <summary>Reintenta el paso (ya contemplado por ReintentosMaximos; valor por defecto).</summary>
    Reintentar = 0,
    /// <summary>Omite el paso y continúa con el siguiente.</summary>
    Omitir = 1,
    /// <summary>Cancela la ejecución del flujo completo.</summary>
    Cancelar = 2,
    /// <summary>Registra la incidencia y continúa (equivalente a Omitir pero con auditoría explícita).</summary>
    RegistrarIncidencia = 3
}
