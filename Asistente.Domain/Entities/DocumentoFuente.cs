namespace Asistente.Domain.Entities;

public class DocumentoFuente
{
    public int IdDocumento { get; set; }
    public int IdFuente { get; set; }
    public bool Activo { get; set; } = true;

    public Documento? Documento { get; set; }
    public FuenteConocimiento? Fuente { get; set; }
}
