using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface ICategoriaDocumentoRepository
{
    Task<CategoriaDocumento?> GetByIdAsync(int id);
    Task<CategoriaDocumento?> GetByNombreAsync(string nombre);
    Task<IEnumerable<CategoriaDocumento>> GetAllAsync();
    Task<IEnumerable<CategoriaDocumento>> GetAllActivasAsync();
    Task AddAsync(CategoriaDocumento categoria);
    void Update(CategoriaDocumento categoria);
}
