using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConfiguracionRAGRepository
{
    Task<ConfiguracionRAG?> GetActivaAsync();
    Task AddAsync(ConfiguracionRAG configuracion);
    void Update(ConfiguracionRAG configuracion);
}
