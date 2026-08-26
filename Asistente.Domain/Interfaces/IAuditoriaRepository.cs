using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IAuditoriaRepository
{
    // Sesiones
    Task<AuditoriaSesion?> GetSesionByIdAsync(int id);
    Task<IEnumerable<AuditoriaSesion>> GetAllSesionesAsync();
    Task AddSesionAsync(AuditoriaSesion sesion);
    void UpdateSesion(AuditoriaSesion sesion);

    // Actividades
    Task<IEnumerable<AuditoriaActividad>> GetAllActividadesAsync();
    Task AddActividadAsync(AuditoriaActividad actividad);
}
