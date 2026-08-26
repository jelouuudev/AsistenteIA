using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Interfaces;

public interface IDocumentoIndexadoRepository
{
    Task<DocumentoIndexado?> GetByIdAsync(int id);
    Task<DocumentoIndexado?> GetByProcesadoIdAsync(int documentoProcesadoId);
    Task<IEnumerable<DocumentoIndexado>> GetAllAsync();
    Task<IEnumerable<DocumentoIndexado>> GetByEstadoAsync(EstadoIndexacion estado);
    Task AddAsync(DocumentoIndexado indexado);
    void Update(DocumentoIndexado indexado);
    void Delete(DocumentoIndexado indexado);
    Task<int> GetTotalDocumentosIndexadosAsync();
    Task<int> GetTotalChunksIndexadosAsync();
    Task<double> GetTiempoPromedioIndexacionAsync();
}
