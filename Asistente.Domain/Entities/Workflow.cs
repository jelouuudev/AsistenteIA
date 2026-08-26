using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

/// <summary>
/// Representa un flujo de trabajo (Workflow) del Motor de Automatización de Procesos Empresariales.
/// Un workflow es reutilizable entre distintos asistentes: se identifica por su código y
/// su disponibilidad está gobernada por su Estado (Activo).
/// </summary>
public class Workflow
{
    public int IdWorkflow { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Frases o palabras clave que disparan este workflow desde el chat (separadas por ;).</summary>
    public string? Disparadores { get; set; }

    public int Version { get; set; } = 1;
    public EstadoWorkflow Estado { get; set; } = EstadoWorkflow.Borrador;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int UsuarioCreacion { get; set; }

    public ICollection<WorkflowPaso> Pasos { get; set; } = new List<WorkflowPaso>();
}
