using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Memoria;

/// <summary>
/// Búsqueda semántica de memorias sobre el IVectorStore compartido (ChromaDB).
/// Cada memoria se indexa con un Guid propio y marcador "MEMORIA:{codigo}":
/// al buscar se filtra en memoria a esos Guids (los documentos RAG nunca se
/// mezclan) y DESPUÉS se aplica el filtro de permisos. Sin embedding o sin
/// vector store, degrada a búsqueda por texto sin romper.
/// </summary>
public class SemanticMemorySearch
{
    private readonly IVectorStore _vectores;
    private readonly IEmbeddingProvider _embeddings;
    private readonly ILogger<SemanticMemorySearch> _logger;

    public SemanticMemorySearch(
        IVectorStore vectores,
        IEmbeddingProvider embeddings,
        ILogger<SemanticMemorySearch> logger)
    {
        _vectores = vectores;
        _embeddings = embeddings;
        _logger = logger;
    }

    public async Task IndexarAsync(MemoryRecord memoria, CancellationToken ct = default)
    {
        try
        {
            var guid = memoria.VectorId ?? Guid.NewGuid();
            var texto = $"{memoria.Titulo}\n{memoria.Contenido}";
            var embedding = await _embeddings.GenerateEmbeddingAsync(texto);
            if (embedding == null || embedding.Length == 0) return;
            await _vectores.IndexAsync(new VectorDocument
            {
                DocumentId = guid,
                Text = texto.Length > 8000 ? texto[..8000] : texto,
                Embedding = embedding,
                MetadataDocumentoNombre = $"MEMORIA:{memoria.Codigo}"
            });
            memoria.VectorId = guid;
            memoria.Indexada = true;
        }
        catch (Exception ex)
        {
            // La indexación nunca tumba la operación: queda pendiente.
            _logger.LogWarning(ex, "Memoria: no se pudo indexar '{Codigo}'.", memoria.Codigo);
            memoria.Indexada = false;
        }
    }

    public async Task DesindexarAsync(MemoryRecord memoria, CancellationToken ct = default)
    {
        if (memoria.VectorId == null) return;
        try { await _vectores.DeleteDocumentAsync(memoria.VectorId.Value); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memoria: no se pudo desindexar '{Codigo}'.", memoria.Codigo);
        }
        memoria.VectorId = null;
        memoria.Indexada = false;
    }

    /// <summary>
    /// Devuelve los Guids de memorias relevantes (sin filtrar permisos: eso lo
    /// hace el Manager, que sí conoce al usuario). Vacío si no hay motor.
    /// </summary>
    public async Task<List<(Guid VectorId, string Texto, double Puntaje)>> BuscarIdsAsync(
        string texto, int topK, CancellationToken ct = default)
    {
        var salida = new List<(Guid, string, double)>();
        try
        {
            var resultados = await _vectores.SearchAsync(texto, Math.Max(topK * 3, topK + 5));
            foreach (var r in resultados)
            {
                if (r.DocumentId == Guid.Empty) continue;
                if (!(r.MetadataDocumentoNombre?.StartsWith("MEMORIA:") == true)) continue;
                salida.Add((r.DocumentId, r.Text ?? string.Empty, r.Score));
                if (salida.Count >= topK * 2) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memoria: búsqueda semántica no disponible.");
        }
        return salida;
    }
}
