namespace Asistente.Shared;

public class ActualizarCategoriaDocumentoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}
