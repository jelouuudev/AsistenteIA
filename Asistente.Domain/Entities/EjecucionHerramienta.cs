using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Registro de auditoría de cada ejecución de una herramienta del orquestador.
/// Toda ejecución (exitosa o fallida) queda registrada obligatoriamente.
/// </summary>
public class EjecucionHerramienta
{
    public int IdEjecucion { get; set; }
    public int IdHerramienta { get; set; }
    public int IdUsuario { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    public string Parametros { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public long TiempoEjecucion { get; set; }
    public string Estado { get; set; } = "Pendiente";

    // Navigation properties
    public Herramienta? Herramienta { get; set; }
    public Usuario? Usuario { get; set; }
}
