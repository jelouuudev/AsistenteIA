using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IOllamaService
{
    Task<string> SendMessageAsync(IEnumerable<Mensaje> historial, CancellationToken cancellationToken = default);
}
