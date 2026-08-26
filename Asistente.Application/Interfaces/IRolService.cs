using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IRolService
{
    Task<RolDto?> ObtenerPorIdAsync(int id);
    Task<RolDto?> ObtenerPorNombreAsync(string nombre);
    Task<IEnumerable<RolDto>> ObtenerTodosAsync();
    Task<RolDto> CrearRolAsync(CrearRolRequest request, int currentUserId, string ipAddress);
    Task<RolDto> ActualizarRolAsync(int id, ActualizarRolRequest request, int currentUserId, string ipAddress);
}
