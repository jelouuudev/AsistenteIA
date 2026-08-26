using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IUsuarioService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task LogoutAsync(int sessionId);
    Task<UsuarioDto?> ObtenerPorIdAsync(int id);
    Task<UsuarioDto?> ObtenerPorNombreAsync(string username);
    Task<IEnumerable<UsuarioDto>> ObtenerTodosAsync();
    Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioRequest request, int currentUserId, string ipAddress);
    Task<UsuarioDto> ActualizarUsuarioAsync(int id, ActualizarUsuarioRequest request, int currentUserId, string ipAddress);
    Task DesactivarUsuarioAsync(int id, int currentUserId, string ipAddress);
    Task CambiarPasswordAsync(int id, CambiarPasswordRequest request, int currentUserId, string ipAddress);
}
