using System;

namespace Asistente.Shared;

public class DocumentoDto
{
    public int IdDocumento { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdCategoria { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public int VersionActual { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool PendienteProcesamiento { get; set; }
    public DateTime FechaRegistro { get; set; }
    public int UsuarioRegistro { get; set; }
}
