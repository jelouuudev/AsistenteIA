using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConexionBaseDatosRepository
{
    Task<ConexionBaseDatos?> GetByIdAsync(int id);
    Task<IEnumerable<ConexionBaseDatos>> GetAllAsync();
    Task<IEnumerable<ConexionBaseDatos>> GetActivasAsync();
    Task AddAsync(ConexionBaseDatos conexion);
    void Update(ConexionBaseDatos conexion);
    void Delete(ConexionBaseDatos conexion);
}
