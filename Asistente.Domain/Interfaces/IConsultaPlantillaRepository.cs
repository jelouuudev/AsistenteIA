using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConsultaPlantillaRepository
{
    Task<ConsultaPlantilla?> GetByIdAsync(int id);
    Task<IEnumerable<ConsultaPlantilla>> GetAllAsync();
    Task<IEnumerable<ConsultaPlantilla>> GetActivasAsync();
    Task<IEnumerable<ConsultaPlantilla>> GetActivasByConexionIdAsync(int idConexion);
    Task AddAsync(ConsultaPlantilla plantilla);
    void Update(ConsultaPlantilla plantilla);
    void Delete(ConsultaPlantilla plantilla);
}
