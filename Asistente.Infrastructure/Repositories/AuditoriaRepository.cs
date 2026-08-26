using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly AsistenteDbContext _context;

    public AuditoriaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<AuditoriaSesion?> GetSesionByIdAsync(int id)
    {
        return await _context.AuditoriasSesion.FindAsync(id);
    }

    public async Task<IEnumerable<AuditoriaSesion>> GetAllSesionesAsync()
    {
        return await _context.AuditoriasSesion
            .Include(s => s.Usuario)
            .OrderByDescending(s => s.FechaInicio)
            .ToListAsync();
    }

    public async Task AddSesionAsync(AuditoriaSesion sesion)
    {
        await _context.AuditoriasSesion.AddAsync(sesion);
    }

    public void UpdateSesion(AuditoriaSesion sesion)
    {
        _context.AuditoriasSesion.Update(sesion);
    }

    public async Task<IEnumerable<AuditoriaActividad>> GetAllActividadesAsync()
    {
        return await _context.AuditoriasActividad
            .Include(a => a.Usuario)
            .OrderByDescending(a => a.FechaHora)
            .ToListAsync();
    }

    public async Task AddActividadAsync(AuditoriaActividad actividad)
    {
        await _context.AuditoriasActividad.AddAsync(actividad);
    }
}
