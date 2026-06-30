using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IChatService
{
    Task<MensajeResponse> ProcesarMensajeAsync(MensajeRequest request, CancellationToken cancellationToken = default);
}
