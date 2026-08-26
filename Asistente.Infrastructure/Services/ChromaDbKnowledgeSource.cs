using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Infrastructure.Services;

public class ChromaDbKnowledgeSource : IKnowledgeSource
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<ChromaDbKnowledgeSource> _logger;

    public string Name => "ChromaDB";

    public ChromaDbKnowledgeSource(IVectorStore vectorStore, ILogger<ChromaDbKnowledgeSource> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<IEnumerable<KnowledgeItem>> SearchAsync(KnowledgeSearchRequest request)
    {
        var filter = new VectorSearchFilter
        {
            IdsFuentes = request.IdsFuentes,
            IdsDocumentos = request.IdsDocumentos,
            IdCategoria = request.IdCategoria,
            TipoFuente = request.TipoFuente,
            IdVersion = request.IdVersion,
            EstadoDocumento = request.EstadoDocumento,
            IncluirHistoricos = request.IncluirHistoricos
        };

        var results = await _vectorStore.SearchWithFilterAsync(request.Query, request.TopK, filter);

        return results
            .Where(r => r.Score >= request.PuntajeMinimo)
            .Select(r => new KnowledgeItem
            {
                Text = r.Text,
                Score = r.Score,
                NombreDocumento = r.MetadataDocumentoNombre,
                CodigoDocumento = r.MetadataDocumentoCodigo,
                PaginaInicial = r.PaginaInicial,
                PaginaFinal = r.PaginaFinal,
                IdDocumento = r.IdDocumento,
                IdVersion = r.IdVersion,
                IdFuente = r.IdFuente,
                VersionDocumento = r.VersionDocumento,
                IdCategoria = r.IdCategoria,
                TipoDocumento = r.TipoDocumento
            })
            .ToList();
    }

    public async Task<bool> IsAvailableAsync()
    {
        return await _vectorStore.HealthCheckAsync();
    }
}
