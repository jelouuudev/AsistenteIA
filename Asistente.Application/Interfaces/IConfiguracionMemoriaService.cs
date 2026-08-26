using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IConfiguracionMemoriaService
{
    Task<ConfiguracionMemoriaDto?> ObtenerActivaAsync();
    Task<ConfiguracionMemoriaDto> ActualizarAsync(ActualizarConfiguracionMemoriaRequest request);
}
