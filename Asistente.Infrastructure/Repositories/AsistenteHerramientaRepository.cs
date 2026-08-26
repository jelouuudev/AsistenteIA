using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class AsistenteHerramientaRepository : IAsistenteHerramientaRepository
{
    private readonly AsistenteDbContext _context;

    public AsistenteHerramientaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Herramienta>> GetHerramientasPorAsistenteAsync(int idAsistente)
        => await _context.AsistentesHerramientas
            .AsNoTracking()
            .Where(ah => ah.IdAsistente == idAsistente && ah.Activa)
            .Include(ah => ah.Herramienta)
            .Where(ah => ah.Herramienta != null && ah.Herramienta.Activa)
            .Select(ah => ah.Herramienta!)
            .OrderBy(h => h.Categoria).ThenBy(h => h.Nombre)
            .ToListAsync();

    public async Task<AsistenteHerramienta?> GetAsync(int idAsistente, int idHerramienta)
        => await _context.AsistentesHerramientas
            .FirstOrDefaultAsync(ah => ah.IdAsistente == idAsistente && ah.IdHerramienta == idHerramienta);

    public async Task<IEnumerable<AsistenteHerramienta>> ObtenerPorHerramientaAsync(int idHerramienta)
        => await _context.AsistentesHerramientas
            .Where(ah => ah.IdHerramienta == idHerramienta)
            .ToListAsync();

    public async Task AddAsync(AsistenteHerramienta relacion)
        => await _context.AsistentesHerramientas.AddAsync(relacion);

    public async Task UpdateAsync(AsistenteHerramienta relacion)
    {
        _context.AsistentesHerramientas.Update(relacion);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(AsistenteHerramienta relacion)
    {
        _context.AsistentesHerramientas.Remove(relacion);
        await Task.CompletedTask;
    }
}
