namespace Asistente.Shared;

public class CrearDocumentoRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdCategoria { get; set; }
}
