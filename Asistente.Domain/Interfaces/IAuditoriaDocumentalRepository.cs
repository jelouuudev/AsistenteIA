using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IAuditoriaDocumentalRepository
{
    Task<IEnumerable<AuditoriaDocumental>> GetByDocumentoIdAsync(int documentoId);
    Task<IEnumerable<AuditoriaDocumental>> GetAllAsync();
    Task AddAsync(AuditoriaDocumental auditoria);
}
