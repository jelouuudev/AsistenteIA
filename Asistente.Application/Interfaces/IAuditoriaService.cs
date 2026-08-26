using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IAuditoriaService
{
    Task<IEnumerable<AuditoriaSesionDto>> ObtenerSesionesAsync();
    Task<IEnumerable<AuditoriaActividadDto>> ObtenerActividadesAsync();
    Task RegistrarActividadAsync(int userId, string modulo, string accion, string descripcion, string? ipAddress);
}
