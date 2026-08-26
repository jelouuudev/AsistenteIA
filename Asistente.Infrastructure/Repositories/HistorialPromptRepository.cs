using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class HistorialPromptRepository : IHistorialPromptRepository
{
    private readonly AsistenteDbContext _context;

    public HistorialPromptRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<HistorialPrompt?> GetByIdAsync(int id)
    {
        return await _context.HistorialPrompts
            .Include(h => h.Prompt)
            .FirstOrDefaultAsync(h => h.IdHistorial == id);
    }

    public async Task<IEnumerable<HistorialPrompt>> GetByPromptIdAsync(int promptId)
    {
        return await _context.HistorialPrompts
            .Include(h => h.Prompt)
            .Where(h => h.IdPrompt == promptId)
            .OrderByDescending(h => h.Version)
            .ToListAsync();
    }

    public async Task<IEnumerable<HistorialPrompt>> GetAllAsync()
    {
        return await _context.HistorialPrompts
            .Include(h => h.Prompt)
            .OrderByDescending(h => h.FechaModificacion)
            .ToListAsync();
    }

    public async Task AddAsync(HistorialPrompt historial)
    {
        await _context.HistorialPrompts.AddAsync(historial);
    }

    public void Delete(HistorialPrompt historial)
    {
        _context.HistorialPrompts.Remove(historial);
    }
}
