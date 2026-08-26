using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConfiguracionMotorConsultasRepository
{
    Task<ConfiguracionMotorConsultas?> GetActivaAsync();
    Task<ConfiguracionMotorConsultas?> GetByIdAsync(int id);
    Task AddAsync(ConfiguracionMotorConsultas config);
    void Update(ConfiguracionMotorConsultas config);
}
