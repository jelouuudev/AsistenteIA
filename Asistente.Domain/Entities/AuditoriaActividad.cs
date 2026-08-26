using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Auditoría integral de operaciones de la plataforma (ETAPA 14 - Actividad 8).
/// Unifica inicio/cierre de sesión, preguntas, herramientas, SQL, workflows, eventos y errores.
/// </summary>
public class AuditoriaActividad
{
    public int IdActividad { get; set; }
    public int IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    public string TipoOperacion { get; set; } = "Operacion"; // Sesion | Consulta | Herramienta | SQL | Workflow | Evento | Configuracion | Error
    public string Modulo { get; set; } = string.Empty;       // Chat | Documentos | Fuentes | Asistentes | Herramientas | Workflows | Auditoria | Configuracion
    public string Accion { get; set; } = string.Empty;       // Login | Logout | Pregunta | Ejecutar | Crear | Modificar | Eliminar
    public string Resultado { get; set; } = "Exitoso";       // Exitoso | Bloqueado | Error
    public string Descripcion { get; set; } = string.Empty;  // detalle de la operación
    public string? DireccionIP { get; set; }
}
