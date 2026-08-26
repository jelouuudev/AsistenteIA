namespace Asistente.Shared;

public class CategoriaDocumentoDto
{
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public int TotalDocumentos { get; set; }
}
