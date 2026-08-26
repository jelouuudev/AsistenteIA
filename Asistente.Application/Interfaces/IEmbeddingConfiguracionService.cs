using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IEmbeddingConfiguracionService
{
    Task<EmbeddingConfiguracionDto?> ObtenerActivaAsync();
    Task<EmbeddingConfiguracionDto?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<EmbeddingConfiguracionDto>> ObtenerTodasAsync();
    Task<EmbeddingConfiguracionDto> ActualizarAsync(int id, ActualizarEmbeddingConfiguracionRequest request);
    Task<EmbeddingConfiguracionDto> CrearAsync(ActualizarEmbeddingConfiguracionRequest request);
}
