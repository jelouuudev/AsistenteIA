using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class PromptSistemaRepository : IPromptSistemaRepository
{
    private readonly AsistenteDbContext _context;

    public PromptSistemaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<PromptSistema?> GetByIdAsync(int id)
    {
        return await _context.PromptsSistema
            .Include(p => p.Asistente)
            .Include(p => p.Historial)
            .FirstOrDefaultAsync(p => p.IdPrompt == id);
    }

    public async Task<PromptSistema?> GetActiveByAsistenteIdAsync(int asistenteId)
    {
        return await _context.PromptsSistema
            .Include(p => p.Asistente)
            .Include(p => p.Historial)
            .Where(p => p.IdAsistente == asistenteId && p.Activo)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PromptSistema>> GetByAsistenteIdAsync(int asistenteId)
    {
        return await _context.PromptsSistema
            .Include(p => p.Asistente)
            .Include(p => p.Historial)
            .Where(p => p.IdAsistente == asistenteId)
            .OrderByDescending(p => p.Version)
            .ToListAsync();
    }

    public async Task<IEnumerable<PromptSistema>> GetAllAsync()
    {
        return await _context.PromptsSistema
            .Include(p => p.Asistente)
            .Include(p => p.Historial)
            .OrderByDescending(p => p.Version)
            .ToListAsync();
    }

    public async Task AddAsync(PromptSistema prompt)
    {
        await _context.PromptsSistema.AddAsync(prompt);
    }

    public void Update(PromptSistema prompt)
    {
        _context.PromptsSistema.Update(prompt);
    }

    public void Delete(PromptSistema prompt)
    {
        _context.PromptsSistema.Remove(prompt);
    }

    public async Task<int> GetNextVersionAsync(int asistenteId)
    {
        var lastVersion = await _context.PromptsSistema
            .Where(p => p.IdAsistente == asistenteId)
            .MaxAsync(p => (int?)p.Version) ?? 0;
        return lastVersion + 1;
    }
}
