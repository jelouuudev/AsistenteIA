using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConsultaEjecutadaRepository
{
    Task<ConsultaEjecutada?> GetByIdAsync(int id);
    Task<IEnumerable<ConsultaEjecutada>> GetAllAsync();
    Task<IEnumerable<ConsultaEjecutada>> GetByUsuarioIdAsync(int idUsuario);
    Task<IEnumerable<ConsultaEjecutada>> GetByConexionIdAsync(int idConexion);
    Task AddAsync(ConsultaEjecutada consulta);
}
