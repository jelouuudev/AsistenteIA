using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IConversacionRepository
{
    Task<Conversacion> CreateAsync(Conversacion conversacion);
    Task<Conversacion?> GetByIdAsync(int id);
    Task UpdateAsync(Conversacion conversacion);
}
