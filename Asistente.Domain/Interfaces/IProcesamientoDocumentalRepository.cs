using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Interfaces;

public interface IProcesamientoDocumentalRepository
{
    Task<DocumentoProcesado?> GetByIdAsync(int id);
    Task<DocumentoProcesado?> GetByIdWithChunksAsync(int id);
    Task<DocumentoProcesado?> GetByVersionIdAsync(int versionId);
    Task<IEnumerable<DocumentoProcesado>> GetAllAsync();
    Task<IEnumerable<DocumentoProcesado>> GetByEstadoAsync(EstadoProcesamiento estado);
    Task<IEnumerable<DocumentoProcesado>> GetPendingDocumentsForProcessingAsync(int maxDocuments);
    Task AddAsync(DocumentoProcesado procesado);
    void Update(DocumentoProcesado procesado);
    Task AddChunksAsync(IEnumerable<DocumentoChunk> chunks);
    Task DeleteChunksByProcesadoIdAsync(int procesadoId);
    Task<int> GetTotalChunksByVersionIdAsync(int versionId);
}
