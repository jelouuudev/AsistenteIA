using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class FuenteConocimientoRepository : IFuenteConocimientoRepository
{
    private readonly AsistenteDbContext _context;

    public FuenteConocimientoRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<FuenteConocimiento?> GetByIdAsync(int id)
    {
        return await _context.FuentesConocimiento
            .Include(f => f.AsistentesFuentes)
            .Include(f => f.DocumentosFuentes)
            .FirstOrDefaultAsync(f => f.IdFuente == id);
    }

    public async Task<IEnumerable<FuenteConocimiento>> GetAllAsync()
    {
        return await _context.FuentesConocimiento
            .Include(f => f.AsistentesFuentes)
            .Include(f => f.DocumentosFuentes)
            .OrderBy(f => f.Prioridad)
            .ThenBy(f => f.Nombre)
            .ToListAsync();
    }

    public async Task<IEnumerable<FuenteConocimiento>> GetActivasAsync()
    {
        return await _context.FuentesConocimiento
            .Where(f => f.Activo)
            .Include(f => f.AsistentesFuentes)
            .Include(f => f.DocumentosFuentes)
            .OrderBy(f => f.Prioridad)
            .ThenBy(f => f.Nombre)
            .ToListAsync();
    }

    public async Task AddAsync(FuenteConocimiento fuente)
    {
        await _context.FuentesConocimiento.AddAsync(fuente);
    }

    public void Update(FuenteConocimiento fuente)
    {
        _context.FuentesConocimiento.Update(fuente);
    }

    public void Delete(FuenteConocimiento fuente)
    {
        _context.FuentesConocimiento.Remove(fuente);
    }
}
