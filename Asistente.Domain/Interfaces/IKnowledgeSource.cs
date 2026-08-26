using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asistente.Domain.Interfaces;

public class KnowledgeSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
    public List<int>? IdsFuentes { get; set; }
    public List<int>? IdsDocumentos { get; set; }
    public int? IdCategoria { get; set; }
    public string? TipoFuente { get; set; }
    public int? IdVersion { get; set; }
    public string? EstadoDocumento { get; set; }
    public bool IncluirHistoricos { get; set; } = false;
    public float PuntajeMinimo { get; set; } = 0.0f;
}

public class KnowledgeItem
{
    public string Text { get; set; } = string.Empty;
    public float Score { get; set; }
    public string? NombreDocumento { get; set; }
    public string? CodigoDocumento { get; set; }
    public int? PaginaInicial { get; set; }
    public int? PaginaFinal { get; set; }
    public int? IdDocumento { get; set; }
    public int? IdVersion { get; set; }
    public int? IdFuente { get; set; }
    public string? NombreFuente { get; set; }
    public string? VersionDocumento { get; set; }
    public int? IdCategoria { get; set; }
    public string? TipoDocumento { get; set; }
}

public interface IKnowledgeSource
{
    Task<IEnumerable<KnowledgeItem>> SearchAsync(KnowledgeSearchRequest request);
    Task<bool> IsAvailableAsync();
    string Name { get; }
}
