using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IAsistenteFuenteRepository
{
    Task<IEnumerable<AsistenteFuente>> GetByAsistenteIdAsync(int idAsistente);
    Task<IEnumerable<AsistenteFuente>> GetByFuenteIdAsync(int idFuente);
    Task<AsistenteFuente?> GetByClaveAsync(int idAsistente, int idFuente);
    Task AddAsync(AsistenteFuente asistenteFuente);
    void Update(AsistenteFuente asistenteFuente);
    void Delete(AsistenteFuente asistenteFuente);
    Task<IEnumerable<FuenteConocimiento>> GetFuentesActivasPorAsistenteAsync(int idAsistente);
}
