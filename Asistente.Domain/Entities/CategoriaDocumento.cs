using System.Collections.Generic;

namespace Asistente.Domain.Entities;

public class CategoriaDocumento
{
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    // Navigation properties
    public ICollection<Documento> Documentos { get; set; } = new List<Documento>();
}
