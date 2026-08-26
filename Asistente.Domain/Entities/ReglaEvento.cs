using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Regla de automatización que asocia un EventoEmpresarial a un Workflow (ETAPA 13 - Actividad 3).
/// La Condicion es una expresión simple opcional que se evalúa contra el contexto del evento
/// (p.ej. "Categoria == 'Error'" o "Prioridad >= 3"). Si queda vacía, la regla siempre aplica.
/// </summary>
public class ReglaEvento
{
    public int IdRegla { get; set; }
    public int IdEvento { get; set; }
    public int IdWorkflow { get; set; }

    /// <summary>Expresión condicional opcional. Vacío = siempre aplica.</summary>
    public string? Condicion { get; set; }

    /// <summary>Mayor número = mayor prioridad. Se evalúan de mayor a menor.</summary>
    public int Prioridad { get; set; } = 1;

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navegación
    public EventoEmpresarial? Evento { get; set; }
    public Workflow? Workflow { get; set; }
}
