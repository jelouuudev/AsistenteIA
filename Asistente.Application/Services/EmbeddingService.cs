using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class EmbeddingService
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IEmbeddingProvider embeddingProvider,
        ILogger<EmbeddingService> logger)
    {
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    public async Task<float[]> GenerarEmbeddingAsync(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("El texto no puede estar vacío para generar embedding.");

        try
        {
            _logger.LogInformation("Generando embedding para texto de {Longitud} caracteres.", texto.Length);

            var embedding = await _embeddingProvider.GenerateEmbeddingAsync(texto);

            if (embedding == null || embedding.Length == 0)
            {
                throw new InvalidOperationException("El proveedor de embeddings devolvió un resultado vacío.");
            }

            _logger.LogInformation("Embedding generado exitosamente. Dimensiones: {Dimensiones}", embedding.Length);
            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar embedding para texto de {Longitud} caracteres.", texto.Length);
            throw;
        }
    }

    public async Task<IEnumerable<(int chunkId, float[] embedding)>> GenerarEmbeddingsLoteAsync(IEnumerable<DocumentoChunk> chunks)
    {
        var resultados = new List<(int chunkId, float[] embedding)>();

        foreach (var chunk in chunks)
        {
            try
            {
                var embedding = await GenerarEmbeddingAsync(chunk.Texto);
                resultados.Add((chunk.IdChunk, embedding));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar embedding para chunk {ChunkId}.", chunk.IdChunk);
            }
        }

        return resultados;
    }
}
