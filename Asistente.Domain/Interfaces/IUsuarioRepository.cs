using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByIdAsync(int id);
    Task<Usuario?> GetByUsernameAsync(string username);
    Task<IEnumerable<Usuario>> GetAllAsync();
    Task AddAsync(Usuario usuario);
    void Update(Usuario usuario);
}
