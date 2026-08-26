using System.Threading.Tasks;

namespace Asistente.Domain.Interfaces;

public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text);
}
