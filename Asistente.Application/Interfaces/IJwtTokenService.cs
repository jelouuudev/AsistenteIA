using System.Collections.Generic;

namespace Asistente.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerarToken(int idUsuario, string nombreUsuario, List<string> roles);
}
