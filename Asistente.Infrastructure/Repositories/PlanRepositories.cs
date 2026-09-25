using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly AsistenteDbContext _context;
    public PlanRepository(AsistenteDbContext context) => _context = context;

    public async Task<Plan> AddAsync(Plan plan, CancellationToken ct = default)
    {
        await _context.Planes.AddAsync(plan, ct);
        await _context.SaveChangesAsync(ct);
        return plan;
    }

    public async Task UpdateAsync(Plan plan, CancellationToken ct = default)
    {
        _context.Planes.Update(plan);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Plan?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.Planes
            .Include(p => p.Pasos)
            .Include(p => p.Dependencias)
            .Include(p => p.Logs)
            .FirstOrDefaultAsync(p => p.IdPlan == id, ct);

    public async Task<List<Plan>> GetRecentAsync(int cantidad = 50, CancellationToken ct = default)
        => await _context.Planes
            .OrderByDescending(p => p.FechaCreacion)
            .Take(cantidad)
            .ToListAsync(ct);

    public async Task<List<Plan>> GetByEstadoAsync(string estado, CancellationToken ct = default)
        => await _context.Planes
            .Where(p => p.Estado == estado)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync(ct);
}

public class PlanStepRepository : IPlanStepRepository
{
    private readonly AsistenteDbContext _context;
    public PlanStepRepository(AsistenteDbContext context) => _context = context;

    public async Task<PlanStep> AddAsync(PlanStep step, CancellationToken ct = default)
    {
        await _context.PlanSteps.AddAsync(step, ct);
        await _context.SaveChangesAsync(ct);
        return step;
    }

    public async Task UpdateAsync(PlanStep step, CancellationToken ct = default)
    {
        _context.PlanSteps.Update(step);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<PlanStep>> GetByPlanAsync(int idPlan, CancellationToken ct = default, bool asNoTracking = false)
    {
        var query = _context.PlanSteps
            .Where(s => s.IdPlan == idPlan)
            .OrderBy(s => s.Orden);
        
        if (asNoTracking)
            return await query.AsNoTracking().ToListAsync(ct);
        
        return await query.ToListAsync(ct);
    }

    public async Task<PlanStep?> GetByIdAsync(int idStep, CancellationToken ct = default)
        => await _context.PlanSteps
            .FirstOrDefaultAsync(s => s.IdStep == idStep, ct);
}

public class PlanDependencyRepository : IPlanDependencyRepository
{
    private readonly AsistenteDbContext _context;
    public PlanDependencyRepository(AsistenteDbContext context) => _context = context;

    public async Task<PlanDependency> AddAsync(PlanDependency dependency, CancellationToken ct = default)
    {
        await _context.PlanDependencies.AddAsync(dependency, ct);
        await _context.SaveChangesAsync(ct);
        return dependency;
    }

    public async Task<List<PlanDependency>> GetByPlanAsync(int idPlan, CancellationToken ct = default)
        => await _context.PlanDependencies
            .Where(d => d.IdPlan == idPlan)
            .ToListAsync(ct);
}

public class PlanExecutionLogRepository : IPlanExecutionLogRepository
{
    private readonly AsistenteDbContext _context;
    public PlanExecutionLogRepository(AsistenteDbContext context) => _context = context;

    public async Task<PlanExecutionLog> AddAsync(PlanExecutionLog log, CancellationToken ct = default)
    {
        await _context.PlanExecutionLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
        return log;
    }

    public async Task<List<PlanExecutionLog>> GetByPlanAsync(int idPlan, CancellationToken ct = default)
        => await _context.PlanExecutionLogs
            .Where(l => l.IdPlan == idPlan)
            .OrderBy(l => l.Fecha)
            .ToListAsync(ct);
}
