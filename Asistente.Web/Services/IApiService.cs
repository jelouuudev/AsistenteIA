using Asistente.Shared;

namespace Asistente.Web.Services;

public interface IApiService
{
    Task<MensajeResponse> EnviarMensajeAsync(MensajeRequest request);
}
