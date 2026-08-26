using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConfiguracionMemoriaRepository
{
    Task<ConfiguracionMemoria?> GetActivaAsync();
    Task<ConfiguracionMemoria> CreateAsync(ConfiguracionMemoria configuracion);
    Task UpdateAsync(ConfiguracionMemoria configuracion);
    Task<IEnumerable<ConfiguracionMemoria>> GetAllAsync();
    Task<ConfiguracionMemoria?> GetByIdAsync(int id);
}
