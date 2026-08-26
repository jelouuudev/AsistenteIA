using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class HerramientaRepository : IHerramientaRepository
{
    private readonly AsistenteDbContext _context;

    public HerramientaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<Herramienta?> GetByIdAsync(int id)
        => await _context.Herramientas.FirstOrDefaultAsync(h => h.IdHerramienta == id);

    public async Task<Herramienta?> GetByCodigoAsync(string codigo)
        => await _context.Herramientas.FirstOrDefaultAsync(h => h.Codigo == codigo);

    public async Task<IEnumerable<Herramienta>> GetAllAsync()
        => await _context.Herramientas.AsNoTracking().OrderBy(h => h.Categoria).ThenBy(h => h.Nombre).ToListAsync();

    public async Task<IEnumerable<Herramienta>> GetActivasAsync()
        => await _context.Herramientas.AsNoTracking()
            .Where(h => h.Activa)
            .OrderBy(h => h.Categoria).ThenBy(h => h.Nombre).ToListAsync();

    public async Task AddAsync(Herramienta herramienta)
        => await _context.Herramientas.AddAsync(herramienta);

    public async Task UpdateAsync(Herramienta herramienta)
    {
        var entry = _context.Herramientas.Update(herramienta);
        await Task.CompletedTask;
    }

    public async Task DeleteByIdAsync(int id)
        => await _context.Herramientas
            .Where(h => h.IdHerramienta == id)
            .ExecuteDeleteAsync();
}
