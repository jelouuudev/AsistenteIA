using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface ITablaAutorizadaRepository
{
    Task<TablaAutorizada?> GetByIdAsync(int id);
    Task<IEnumerable<TablaAutorizada>> GetByConexionIdAsync(int idConexion);
    Task<IEnumerable<TablaAutorizada>> GetActivasByConexionIdAsync(int idConexion);
    Task AddAsync(TablaAutorizada tabla);
    void Update(TablaAutorizada tabla);
    void Delete(TablaAutorizada tabla);
}
