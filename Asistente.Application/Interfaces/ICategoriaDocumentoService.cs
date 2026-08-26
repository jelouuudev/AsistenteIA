using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface ICategoriaDocumentoService
{
    Task<CategoriaDocumentoDto?> ObtenerPorIdAsync(int id);
    Task<IEnumerable<CategoriaDocumentoDto>> ObtenerTodasAsync();
    Task<IEnumerable<CategoriaDocumentoDto>> ObtenerActivasAsync();
    Task<CategoriaDocumentoDto> CrearAsync(CrearCategoriaDocumentoRequest request, int currentUserId, string ipAddress);
    Task<CategoriaDocumentoDto> ActualizarAsync(int id, ActualizarCategoriaDocumentoRequest request, int currentUserId, string ipAddress);
}
