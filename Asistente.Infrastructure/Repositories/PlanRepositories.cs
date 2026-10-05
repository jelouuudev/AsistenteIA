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
        // Solo escalares del plan: los hijos (Pasos/Dependencias/Logs) los gestionan
        // sus propios repositorios. El anterior _context.Planes.Update(plan) marcaba
        // TODO el grafo como Modified y pisaba con valores stale los resultados que
        // las ramas paralelas persistían desde otros scopes (pasos volvían a
        // Pendiente/NULL pese al log de éxito).
        _context.Entry(plan).State = EntityState.Modified;
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateEstadoAsync(int idPlan, string estado, DateTime? fechaFin, CancellationToken ct = default)
    {
        // UPDATE directo sin cargar el grafo: imposible contaminar pasos.
        await _context.Planes
            .Where(p => p.IdPlan == idPlan)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Estado, estado)
                .SetProperty(p => p.FechaFin, fechaFin), ct);

        // ExecuteUpdateAsync no refresca el ChangeTracker: si el Plan sigue trackeado
        // con el estado anterior, cualquier SaveChanges posterior reescribiría el
        // valor viejo (el plan se quedaba en IniciandoEjecucion para siempre) y la
        // siguiente GetByIdAsync devolvería un snapshot obsoleto (era el bug: /ejecutar
        // respondía "Borrador" con todos los pasos en Pendiente).
        var tracked = _context.ChangeTracker.Entries<Plan>()
            .FirstOrDefault(e => e.Entity.IdPlan == idPlan);
        if (tracked != null)
        {
            // SetValues fija valor actual Y original (deja la propiedad sin modificar),
            // así la entidad refleja la BD sin que un SaveChanges posterior reescriba
            // la fila. No usar IsModified=false: eso revierte el valor actual al
            // original y volvería a dejar el estado viejo en memoria.
            // El ExecuteUpdate SIEMPRE escribe FechaFin (a null si no se envía), así que
            // aquí también: si se omitiera, la BD y la memoria divergirían.
            tracked.CurrentValues.SetValues(new { Estado = estado, FechaFin = fechaFin });
        }
    }

    public async Task<Plan?> GetByIdAsync(int id, CancellationToken ct = default)
        // TRACKING a propósito (no AsNoTracking): el llamador muta la entidad y la
        // guarda con UpdateAsync, y sin tracking EF no lleva snapshot de la colección
        // Plan.Pasos; al fixupear los PlanStep ya rastreados los AGREGA de nuevo a la
        // lista y cada paso aparece DUPLICADO (el coordinador reachaba a listar
        // "Paso 0, Paso 0, Paso 1, Paso 1..."). La coherencia del read-after-write la
        // garantiza la sincronización del tracker en UpdateEstadoAsync.
        // AsSplitQuery evita el cartesiano de las 3 colecciones incluidas (y el warning
        // MultipleCollectionInclude) sin perder el fixup de navegaciones.
        => await _context.Planes
            .AsSplitQuery()
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
        // Solo escalares del paso: DbSet.Update(step) hacía graph-attach y marcaba
        // como Modified TODAS las entidades alcanzables (paso.Plan + pasos hermanos
        // stale). El background actualiza el paso Agent con la entidad stale de
        // plan.Pasos y SaveChanges sobrescribía las filas Tool (Completado → Pendiente
        // + Resultado NULL) y el Plan (→ IniciandoEjecucion). Misma corrección que
        // PlanRepository.UpdateAsync.
        var tracked = _context.ChangeTracker.Entries<PlanStep>()
            .FirstOrDefault(e => e.Entity.IdStep == step.IdStep);
        if (tracked != null)
        {
            // Ya rastreada en este contexto: copiar solo escalares, sin grafo.
            tracked.CurrentValues.SetValues(step);
        }
        else
        {
            // Entidad de otro scope/contexto: romper el grafo antes de adjuntar.
            step.Plan = null;
            _context.Entry(step).State = EntityState.Modified;
        }
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
