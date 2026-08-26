using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Permiso granular por módulo (ETAPA 14 - Actividad 3).
/// Ejemplos de Código: CHAT_CONSULTAR, SQL_ADMINISTRAR, WORKFLOWS_EJECUTAR, etc.
/// </summary>
public class Permiso
{
    public int IdPermiso { get; set; }
    public string Codigo { get; set; } = string.Empty; // único
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
