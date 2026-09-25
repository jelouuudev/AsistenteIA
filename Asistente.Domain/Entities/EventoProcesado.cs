using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Registro de auditoría de cada ejecución automática disparada por un evento (ETAPA 13 - Actividades 4/9).
/// Permite monitorear y auditar todas las ejecuciones sin intervención del usuario.
/// </summary>
public class EventoProcesado
{
    public int IdEventoProcesado { get; set; }
    public int IdEvento { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    /// <summary>Estado final del procesamiento: Pendiente, EnProceso, Completado, Error, Reintentando.</summary>
    public string Estado { get; set; } = "Pendiente";

    /// <summary>Mensaje de resultado o descripción del error.</summary>
    public string? Resultado { get; set; }

    /// <summary>
    /// Datos JSON con los que se disparó el evento (lo que el evento "fue" realmente).
    /// Se preserva intacto: Resultado se sobrescribe durante el procesamiento.
    /// </summary>
    public string? ContextoDisparo { get; set; }

    /// <summary>Tiempo total de procesamiento en milisegundos.</summary>
    public long TiempoProcesamiento { get; set; }

    public int? IdRegla { get; set; }
    public int? IdWorkflow { get; set; }
    public int? IdUsuario { get; set; }

    // Navegación
    public EventoEmpresarial? Evento { get; set; }
    public Workflow? Workflow { get; set; }
}
