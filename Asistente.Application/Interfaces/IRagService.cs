using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IRagService
{
    Task<RagContextoDto> RecuperarContextoDocumentalAsync(string consulta, int? topK = null, string? savedDocumentPreference = null);
    Task<BusquedaSemanticaResponse> BuscarSemanticamenteAsync(string consulta, int? topK = null);
}
