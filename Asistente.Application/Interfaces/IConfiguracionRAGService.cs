using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IConfiguracionRAGService
{
    Task<ConfiguracionRAGDto> ObtenerActivaAsync();
    Task<ConfiguracionRAGDto> ActualizarAsync(ActualizarConfiguracionRAGRequest request);
}
