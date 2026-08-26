namespace Asistente.Shared;

public class ReindexarRequest
{
    public int? IdDocumentoProcesado { get; set; }
    public int? IdCategoria { get; set; }
    public bool Todos { get; set; } = false;
}
