using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IEmbeddingConfiguracionRepository
{
    Task<EmbeddingConfiguracion?> GetActivaAsync();
    Task<EmbeddingConfiguracion?> GetByIdAsync(int id);
    Task<IEnumerable<EmbeddingConfiguracion>> GetAllAsync();
    Task AddAsync(EmbeddingConfiguracion configuracion);
    void Update(EmbeddingConfiguracion configuracion);
}
