using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

/// <summary>
/// Evento empresarial que puede disparar automatizaciones (ETAPA 13 - Actividad 1/2).
/// Ejemplos de categorías: Sistema, Documento, Seguridad, Negocio, Programado.
/// </summary>
public class EventoEmpresarial
{
    public int IdEvento { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Categoria { get; set; } = "Sistema";
    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int UsuarioCreacion { get; set; } = 1;

    public ICollection<ReglaEvento> Reglas { get; set; } = new List<ReglaEvento>();
}
