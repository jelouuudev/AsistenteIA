using System.Threading.Tasks;

namespace Asistente.Domain.Interfaces;

public interface IExtractoraTextoService
{
    Task<ExtraccionPdfResult> ExtraerTextoPdfAsync(string rutaArchivo);
    Task<ExtraccionPdfResult> ExtraerTextoAsync(string rutaArchivo);
}

public class ExtraccionPdfResult
{
    public bool Exitoso { get; set; }
    public int TotalPaginas { get; set; }
    public string TextoCompleto { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool EsDocumentoProtegido { get; set; }
}
