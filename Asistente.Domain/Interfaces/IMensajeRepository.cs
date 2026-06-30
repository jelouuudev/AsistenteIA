using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IMensajeRepository
{
    Task<Mensaje> CreateAsync(Mensaje mensaje);
    Task<IEnumerable<Mensaje>> GetByConversacionIdAsync(int conversacionId);
}
