using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IDocumentoVersionRepository
{
    Task<DocumentoVersion?> GetByIdAsync(int id);
    Task<IEnumerable<DocumentoVersion>> GetByDocumentoIdAsync(int documentoId);
    Task<DocumentoVersion?> GetVersionActivaAsync(int documentoId);
    Task AddAsync(DocumentoVersion version);
    void Update(DocumentoVersion version);
}
