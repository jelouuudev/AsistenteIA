using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConversacionRepository
{
    Task<Conversacion> CreateAsync(Conversacion conversacion);
    Task<Conversacion?> GetByIdAsync(int id);
    Task UpdateAsync(Conversacion conversacion);
    Task<IEnumerable<Conversacion>> GetByUsuarioAsync(int usuarioId);
    Task<IEnumerable<Conversacion>> GetAllAsync();
    Task<IEnumerable<Conversacion>> SearchByTituloAsync(int usuarioId, string titulo);
    Task<bool> ExistsAsync(int id);
}
