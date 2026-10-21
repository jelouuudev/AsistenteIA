using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class MemoryRepository : IMemoryRepository
{
    private readonly AsistenteDbContext _context;
    public MemoryRepository(AsistenteDbContext context) => _context = context;

    public Task<MemoryRecord?> GetByIdAsync(int id, CancellationToken ct = default)
        => _context.MemoryRecords
            .Include(m => m.Scope).Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.IdMemory == id, ct);

    public Task<MemoryRecord?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        => _context.MemoryRecords
            .Include(m => m.Scope).Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Codigo == codigo, ct);

    public Task<List<MemoryRecord>> GetActivasAsync(CancellationToken ct = default)
        => _context.MemoryRecords
            .Include(m => m.Scope).Include(m => m.Category)
            .Where(m => m.Activo).OrderByDescending(m => m.FechaActualizacion).ToListAsync(ct);

    public Task<List<MemoryRecord>> GetByScopeAsync(int idScope, CancellationToken ct = default)
        => _context.MemoryRecords
            .Include(m => m.Scope).Include(m => m.Category)
            .Where(m => m.IdScope == idScope && m.Activo).ToListAsync(ct);

    public async Task<MemoryRecord> AddAsync(MemoryRecord memoria, CancellationToken ct = default)
    {
        await _context.MemoryRecords.AddAsync(memoria, ct);
        await _context.SaveChangesAsync(ct);
        return memoria;
    }

    public async Task UpdateAsync(MemoryRecord memoria, CancellationToken ct = default)
    {
        _context.Entry(memoria).State = EntityState.Modified;
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MemoryRecord memoria, CancellationToken ct = default)
    {
        _context.MemoryRecords.Remove(memoria);
        await _context.SaveChangesAsync(ct);
    }

    public Task<List<MemoryScope>> GetScopesAsync(CancellationToken ct = default)
        => _context.MemoryScopes.Where(s => s.Activo).OrderBy(s => s.IdScope).ToListAsync(ct);

    public Task<List<MemoryCategory>> GetCategoriesAsync(CancellationToken ct = default)
        => _context.MemoryCategories.Where(c => c.Activo).OrderBy(c => c.Nombre).ToListAsync(ct);

    public Task<List<MemoryRetentionPolicy>> GetPoliticasAsync(CancellationToken ct = default)
        => _context.MemoryRetentionPolicies.Where(p => p.Activo).ToListAsync(ct);

    /// <summary>
    /// El dueño debe existir en su tabla: Usuario → Usuario, Agente → Asistente,
    /// Workflow → Workflow. Los ámbitos compartidos no tienen dueño.
    /// </summary>
    public Task<bool> PropietarioExisteAsync(string ambito, int idPropietario, CancellationToken ct = default)
        => (ambito ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "USUARIO" => _context.Usuarios.AnyAsync(u => u.IdUsuario == idPropietario, ct),
            "AGENTE" => _context.Asistentes.AnyAsync(a => a.IdAsistente == idPropietario, ct),
            "WORKFLOW" => _context.Workflows.AnyAsync(w => w.IdWorkflow == idPropietario, ct),
            _ => Task.FromResult(false)
        };
}

public class MemoryVersionRepository : IMemoryVersionRepository
{
    private readonly AsistenteDbContext _context;
    public MemoryVersionRepository(AsistenteDbContext context) => _context = context;

    public async Task AddAsync(MemoryVersion version, CancellationToken ct = default)
    {
        await _context.MemoryVersions.AddAsync(version, ct);
        await _context.SaveChangesAsync(ct);
    }

    public Task<List<MemoryVersion>> GetByMemoryAsync(int idMemory, CancellationToken ct = default)
        => _context.MemoryVersions.Where(v => v.IdMemory == idMemory)
            .OrderBy(v => v.Numero).ToListAsync(ct);

    public Task<MemoryVersion?> GetByNumeroAsync(int idMemory, int numero, CancellationToken ct = default)
        => _context.MemoryVersions
            .FirstOrDefaultAsync(v => v.IdMemory == idMemory && v.Numero == numero, ct);
}

public class MemoryAccessLogRepository : IMemoryAccessLogRepository
{
    private readonly AsistenteDbContext _context;
    public MemoryAccessLogRepository(AsistenteDbContext context) => _context = context;

    public async Task AddAsync(MemoryAccessLog log, CancellationToken ct = default)
    {
        await _context.MemoryAccessLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
    }

    public Task<List<MemoryAccessLog>> GetByMemoryAsync(int idMemory, int tope = 100, CancellationToken ct = default)
        => _context.MemoryAccessLogs.Where(l => l.IdMemory == idMemory)
            .OrderByDescending(l => l.Fecha).Take(tope).ToListAsync(ct);

    public Task<List<MemoryAccessLog>> GetRecientesAsync(int tope = 50, CancellationToken ct = default)
        => _context.MemoryAccessLogs
            .OrderByDescending(l => l.Fecha).Take(tope).ToListAsync(ct);

    public Task<long> ContarPorOperacionAsync(string operacion, CancellationToken ct = default)
        => _context.MemoryAccessLogs.LongCountAsync(l => l.Operacion == operacion, ct);

    public async Task<List<(int? Agente, long Total)>> ContarPorAgenteAsync(CancellationToken ct = default)
    {
        var filas = await _context.MemoryAccessLogs
            .GroupBy(l => l.IdAsistente)
            .Select(g => new { Agente = g.Key, Total = g.LongCount() })
            .ToListAsync(ct);
        return filas.Select(f => ((int?)f.Agente, (long)f.Total)).ToList();
    }

    public async Task<List<(int? Usuario, long Total)>> ContarPorUsuarioAsync(CancellationToken ct = default)
    {
        var filas = await _context.MemoryAccessLogs
            .GroupBy(l => l.IdUsuario)
            .Select(g => new { Usuario = g.Key, Total = g.LongCount() })
            .ToListAsync(ct);
        return filas.Select(f => ((int?)f.Usuario, (long)f.Total)).ToList();
    }

    public async Task<double?> DuracionMediaMsAsync(string operacion, CancellationToken ct = default)
    {
        // AverageAsync devuelve 0 (no null) cuando no hay filas: se traduce a null
        // para que el panel pueda distinguir "sin medir" de "instantáneo".
        var media = await _context.MemoryAccessLogs
            .Where(l => l.Operacion == operacion && l.DuracionMs != null)
            .Select(l => (double)l.DuracionMs!)
            .ToListAsync(ct);
        return media.Count == 0 ? null : media.Average();
    }

    public async Task<List<(string? Ambito, long Total)>> ContarPorTipoAsync(CancellationToken ct = default)
    {
        var filas = await _context.MemoryAccessLogs
            .GroupBy(l => l.Ambito)
            .Select(g => new { Ambito = g.Key, Total = g.LongCount() })
            .ToListAsync(ct);
        return filas.Select(f => ((string?)f.Ambito, (long)f.Total)).ToList();
    }
}
