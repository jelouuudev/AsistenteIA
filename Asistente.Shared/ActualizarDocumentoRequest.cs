namespace Asistente.Shared;

public class ActualizarDocumentoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdCategoria { get; set; }
}
