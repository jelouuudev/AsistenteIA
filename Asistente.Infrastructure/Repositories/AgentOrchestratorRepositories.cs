using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class AgentExecutionRepository : IAgentExecutionRepository
{
    private readonly AsistenteDbContext _context;
    public AgentExecutionRepository(AsistenteDbContext context) => _context = context;

    public async Task<AgentExecution> AddAsync(AgentExecution execution, CancellationToken ct = default)
    {
        await _context.AgentExecutions.AddAsync(execution, ct);
        await _context.SaveChangesAsync(ct);
        return execution;
    }

    public async Task UpdateAsync(AgentExecution execution, CancellationToken ct = default)
    {
        _context.AgentExecutions.Update(execution);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<AgentExecution?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.AgentExecutions
            .Include(e => e.Pasos)
            .Include(e => e.Trazas)
            .Include(e => e.AgentePrincipal)
            .FirstOrDefaultAsync(e => e.IdExecution == id, ct);

    public async Task<List<AgentExecution>> GetRecentAsync(int cantidad = 50, CancellationToken ct = default)
        => await _context.AgentExecutions
            .Include(e => e.AgentePrincipal)
            .OrderByDescending(e => e.FechaInicio)
            .Take(cantidad)
            .ToListAsync(ct);
}

public class AgentExecutionStepRepository : IAgentExecutionStepRepository
{
    private readonly AsistenteDbContext _context;
    public AgentExecutionStepRepository(AsistenteDbContext context) => _context = context;

    public async Task<AgentExecutionStep> AddAsync(AgentExecutionStep step, CancellationToken ct = default)
    {
        await _context.AgentExecutionSteps.AddAsync(step, ct);
        await _context.SaveChangesAsync(ct);
        return step;
    }

    public async Task UpdateAsync(AgentExecutionStep step, CancellationToken ct = default)
    {
        _context.AgentExecutionSteps.Update(step);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<AgentExecutionStep>> GetByExecutionAsync(int idExecution, CancellationToken ct = default)
        => await _context.AgentExecutionSteps
            .Where(s => s.IdExecution == idExecution)
            .OrderBy(s => s.Orden)
            .ToListAsync(ct);
}

public class AgentExecutionTraceRepository : IAgentExecutionTraceRepository
{
    private readonly AsistenteDbContext _context;
    public AgentExecutionTraceRepository(AsistenteDbContext context) => _context = context;

    public async Task<AgentExecutionTrace> AddAsync(AgentExecutionTrace trace, CancellationToken ct = default)
    {
        await _context.AgentExecutionTraces.AddAsync(trace, ct);
        await _context.SaveChangesAsync(ct);
        return trace;
    }

    public async Task<List<AgentExecutionTrace>> GetByExecutionAsync(int idExecution, CancellationToken ct = default)
        => await _context.AgentExecutionTraces
            .Where(t => t.IdExecution == idExecution)
            .OrderBy(t => t.FechaHora)
            .ToListAsync(ct);
}

public class AgentCollaborationRuleRepository : IAgentCollaborationRuleRepository
{
    private readonly AsistenteDbContext _context;
    public AgentCollaborationRuleRepository(AsistenteDbContext context) => _context = context;

    public async Task<List<AgentCollaborationRule>> GetAllAsync(CancellationToken ct = default)
        => await _context.AgentCollaborationRules
            .Include(r => r.Origen).Include(r => r.Destino)
            .OrderBy(r => r.Prioridad).ToListAsync(ct);

    public async Task<List<AgentCollaborationRule>> GetActivasAsync(CancellationToken ct = default)
        => await _context.AgentCollaborationRules
            .Where(r => r.Activa)
            .Include(r => r.Origen).Include(r => r.Destino)
            .OrderBy(r => r.Prioridad).ToListAsync(ct);

    public async Task<AgentCollaborationRule?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.AgentCollaborationRules
            .Include(r => r.Origen).Include(r => r.Destino)
            .FirstOrDefaultAsync(r => r.IdRule == id, ct);

    public async Task<AgentCollaborationRule> AddAsync(AgentCollaborationRule rule, CancellationToken ct = default)
    {
        await _context.AgentCollaborationRules.AddAsync(rule, ct);
        await _context.SaveChangesAsync(ct);
        return rule;
    }

    public async Task UpdateAsync(AgentCollaborationRule rule, CancellationToken ct = default)
    {
        _context.AgentCollaborationRules.Update(rule);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var rule = await _context.AgentCollaborationRules.FindAsync(new object[] { id }, ct);
        if (rule != null)
        {
            _context.AgentCollaborationRules.Remove(rule);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> EstaPermitidoAsync(int origen, int destino, CancellationToken ct = default)
    {
        // Regla explícita de denegación tiene prioridad
        var regla = await _context.AgentCollaborationRules
            .Where(r => r.Activa && r.AgenteOrigen == origen && r.AgenteDestino == destino)
            .OrderBy(r => r.Prioridad)
            .FirstOrDefaultAsync(ct);

        if (regla != null)
            return regla.Permitido;

        // Sin regla explícita: por defecto NO permitido (deny by default, Regla 3)
        return false;
    }
}
