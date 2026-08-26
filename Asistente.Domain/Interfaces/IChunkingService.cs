using System.Collections.Generic;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IChunkingService
{
    List<DocumentoChunk> DividirEnChunks(
        string textoNormalizado,
        int idDocumentoProcesado,
        ChunkingConfig config);
}

public class ChunkingConfig
{
    public int TamanoMaximoChunk { get; set; } = 1000;
    public int Solapamiento { get; set; } = 200;
    public int LongitudMinima { get; set; } = 100;
}
