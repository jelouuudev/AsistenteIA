using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IRolRepository
{
    Task<Rol?> GetByIdAsync(int id);
    Task<Rol?> GetByNombreAsync(string nombre);
    Task<IEnumerable<Rol>> GetAllAsync();
    Task AddAsync(Rol rol);
    void Update(Rol rol);
}
