using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IVistaAutorizadaRepository
{
    Task<VistaAutorizada?> GetByIdAsync(int id);
    Task<IEnumerable<VistaAutorizada>> GetByConexionIdAsync(int idConexion);
    Task<IEnumerable<VistaAutorizada>> GetActivasByConexionIdAsync(int idConexion);
    Task AddAsync(VistaAutorizada vista);
    void Update(VistaAutorizada vista);
    void Delete(VistaAutorizada vista);
}
