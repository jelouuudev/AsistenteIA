namespace Asistente.Domain.Entities.Aprobaciones;

/// <summary>
/// Estados del ciclo de vida de una solicitud de aprobación (ETAPA 19, punto 6).
/// </summary>
public enum EstadoAprobacion
{
    Pendiente,
    EnRevision,
    Aprobado,
    Rechazado,
    Cancelado,
    Expirado,
    Delegado
}

/// <summary>
/// Tipos de aprobación soportados por la plataforma (ETAPA 19, punto 7).
/// </summary>
public enum TipoAprobacion
{
    Operacional,   // Ejecutar un workflow
    Financiera,    // Generar un reporte financiero
    Administrativa,// Cambiar configuración
    Seguridad,     // Acceder a información restringida
    Publicacion,   // Publicar un agente
    Manual         // Cualquier aprobación personalizada
}
