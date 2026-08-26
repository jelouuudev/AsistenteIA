using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IFuenteConocimientoRepository
{
    Task<FuenteConocimiento?> GetByIdAsync(int id);
    Task<IEnumerable<FuenteConocimiento>> GetAllAsync();
    Task<IEnumerable<FuenteConocimiento>> GetActivasAsync();
    Task AddAsync(FuenteConocimiento fuente);
    void Update(FuenteConocimiento fuente);
    void Delete(FuenteConocimiento fuente);
}
