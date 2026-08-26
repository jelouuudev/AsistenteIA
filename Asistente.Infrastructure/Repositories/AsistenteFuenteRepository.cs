using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class AsistenteFuenteRepository : IAsistenteFuenteRepository
{
    private readonly AsistenteDbContext _context;

    public AsistenteFuenteRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AsistenteFuente>> GetByAsistenteIdAsync(int idAsistente)
    {
        return await _context.AsistentesFuentes
            .Include(af => af.Fuente)
            .Where(af => af.IdAsistente == idAsistente)
            .ToListAsync();
    }

    public async Task<IEnumerable<AsistenteFuente>> GetByFuenteIdAsync(int idFuente)
    {
        return await _context.AsistentesFuentes
            .Include(af => af.Asistente)
            .Where(af => af.IdFuente == idFuente)
            .ToListAsync();
    }

    public async Task<AsistenteFuente?> GetByClaveAsync(int idAsistente, int idFuente)
    {
        return await _context.AsistentesFuentes
            .Include(af => af.Fuente)
            .FirstOrDefaultAsync(af => af.IdAsistente == idAsistente && af.IdFuente == idFuente);
    }

    public async Task AddAsync(AsistenteFuente asistenteFuente)
    {
        await _context.AsistentesFuentes.AddAsync(asistenteFuente);
    }

    public void Update(AsistenteFuente asistenteFuente)
    {
        _context.AsistentesFuentes.Update(asistenteFuente);
    }

    public void Delete(AsistenteFuente asistenteFuente)
    {
        _context.AsistentesFuentes.Remove(asistenteFuente);
    }

    public async Task<IEnumerable<FuenteConocimiento>> GetFuentesActivasPorAsistenteAsync(int idAsistente)
    {
        return await _context.AsistentesFuentes
            .Where(af => af.IdAsistente == idAsistente && af.Activo && af.Fuente!.Activo)
            .Select(af => af.Fuente!)
            .OrderBy(f => f.Prioridad)
            .ToListAsync();
    }
}
