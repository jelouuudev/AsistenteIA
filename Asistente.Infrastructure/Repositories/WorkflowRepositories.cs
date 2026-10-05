using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly AsistenteDbContext _context;
    public WorkflowRepository(AsistenteDbContext context) => _context = context;

    public async Task<Workflow?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.Workflows.AsNoTracking()
            .Include(w => w.Pasos.OrderBy(p => p.Orden))
            .FirstOrDefaultAsync(w => w.IdWorkflow == id, ct);

    public async Task<Workflow?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        => await _context.Workflows.AsNoTracking()
            .Include(w => w.Pasos.OrderBy(p => p.Orden))
            .FirstOrDefaultAsync(w => w.Codigo == codigo, ct);

    public async Task<IEnumerable<Workflow>> GetAllAsync(CancellationToken ct = default)
        => await _context.Workflows.AsNoTracking()
            .Include(w => w.Pasos.OrderBy(p => p.Orden))
            .OrderBy(w => w.Nombre).ToListAsync(ct);

    public async Task<IEnumerable<Workflow>> GetActivosAsync(CancellationToken ct = default)
        => await _context.Workflows.AsNoTracking()
            .Where(w => w.Estado == EstadoWorkflow.Activo)
            .OrderBy(w => w.Nombre).ToListAsync(ct);

    public async Task AddAsync(Workflow workflow, CancellationToken ct = default)
        => await _context.Workflows.AddAsync(workflow, ct);

    public async Task UpdateAsync(Workflow workflow, CancellationToken ct = default)
    {
        _context.Workflows.Update(workflow);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Workflow workflow, CancellationToken ct = default)
    {
        _context.Workflows.Remove(workflow);
        await Task.CompletedTask;
    }

    public async Task DeleteByIdAsync(int id, CancellationToken ct = default)
    {
        // DELETE directo por ID (ExecuteDeleteAsync) SIN cláusula RowVersion.
        // Evita la DbUpdateConcurrencyException que ocurría cuando el background
        // service (Quartz) actualizaba el workflow entre el GetById (AsNoTracking)
        // y el SaveChanges, invalidando el RowVersion de la entidad desadjuntada.
        await _context.Workflows
            .Where(w => w.IdWorkflow == id)
            .ExecuteDeleteAsync(ct);
    }
}

public class WorkflowPasoRepository : IWorkflowPasoRepository
{
    private readonly AsistenteDbContext _context;
    public WorkflowPasoRepository(AsistenteDbContext context) => _context = context;

    public async Task<WorkflowPaso?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.WorkflowPasos.FirstOrDefaultAsync(p => p.IdPaso == id, ct);

    public async Task<IEnumerable<WorkflowPaso>> GetByWorkflowAsync(int idWorkflow, CancellationToken ct = default)
        => await _context.WorkflowPasos.AsNoTracking()
            .Where(p => p.IdWorkflow == idWorkflow)
            .OrderBy(p => p.Orden).ToListAsync(ct);

    public async Task AddAsync(WorkflowPaso paso, CancellationToken ct = default)
        => await _context.WorkflowPasos.AddAsync(paso, ct);

    public async Task UpdateAsync(WorkflowPaso paso, CancellationToken ct = default)
    {
        _context.WorkflowPasos.Update(paso);
        await Task.CompletedTask;
    }

    public async Task DeleteByWorkflowAsync(int idWorkflow, CancellationToken ct = default)
    {
        // DELETE directo en SQL (ExecuteDeleteAsync) para garantizar que los pasos
        // viejos se eliminen de verdad, sin depender del seguimiento de cambios del
        // contexto (que en ActualizarAsync permitía que se duplicaran al re-guardar).
        // NOTA: confirma de inmediato (fuera de transacción). No usar en flujos que
        // deban ser atómicos; para eso están GetTrackedByWorkflowAsync + RemoveRange.
        await _context.WorkflowPasos
            .Where(p => p.IdWorkflow == idWorkflow)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<List<WorkflowPaso>> GetTrackedByWorkflowAsync(int idWorkflow, CancellationToken ct = default)
        => await _context.WorkflowPasos
            .Where(p => p.IdWorkflow == idWorkflow)
            .OrderBy(p => p.Orden).ToListAsync(ct);

    public void RemoveRange(IEnumerable<WorkflowPaso> pasos)
        => _context.WorkflowPasos.RemoveRange(pasos);
}

public class WorkflowEjecucionRepository : IWorkflowEjecucionRepository
{
    private readonly AsistenteDbContext _context;
    public WorkflowEjecucionRepository(AsistenteDbContext context) => _context = context;

    public async Task<long> CountAsync(CancellationToken ct = default)
        => await _context.WorkflowEjecuciones.LongCountAsync(ct);

    public async Task<WorkflowEjecucion?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.WorkflowEjecuciones.AsNoTracking()
            .Include(e => e.PasosEjecucion)
            .FirstOrDefaultAsync(e => e.IdEjecucion == id, ct);

    public async Task<IEnumerable<WorkflowEjecucion>> GetAllAsync(CancellationToken ct = default)
        => await _context.WorkflowEjecuciones.AsNoTracking()
            .OrderByDescending(e => e.FechaInicio).ToListAsync(ct);

    public async Task<IEnumerable<WorkflowEjecucion>> GetByWorkflowAsync(int idWorkflow, CancellationToken ct = default)
        => await _context.WorkflowEjecuciones.AsNoTracking()
            .Where(e => e.IdWorkflow == idWorkflow)
            .OrderByDescending(e => e.FechaInicio).ToListAsync(ct);

    public async Task<WorkflowEjecucion?> GetPendienteConfirmacionAsync(int idUsuario, CancellationToken ct = default)
        => await _context.WorkflowEjecuciones
            .Where(e => e.IdUsuario == idUsuario && e.Estado == "RequiereConfirmacion")
            .OrderByDescending(e => e.FechaInicio)
            .FirstOrDefaultAsync(ct);

    public async Task<WorkflowEjecucion?> GetByIdForUpdateAsync(int id, CancellationToken ct = default)
        => await _context.WorkflowEjecuciones
            .Include(e => e.PasosEjecucion)
            .FirstOrDefaultAsync(e => e.IdEjecucion == id, ct);

    public async Task AddAsync(WorkflowEjecucion ejecucion, CancellationToken ct = default)
        => await _context.WorkflowEjecuciones.AddAsync(ejecucion, ct);

    public async Task UpdateAsync(WorkflowEjecucion ejecucion, CancellationToken ct = default)
    {
        _context.WorkflowEjecuciones.Update(ejecucion);
        await Task.CompletedTask;
    }
}

public class WorkflowPasoEjecucionRepository : IWorkflowPasoEjecucionRepository
{
    private readonly AsistenteDbContext _context;
    public WorkflowPasoEjecucionRepository(AsistenteDbContext context) => _context = context;

    public async Task<IEnumerable<WorkflowPasoEjecucion>> GetByEjecucionAsync(int idEjecucion, CancellationToken ct = default)
        => await _context.WorkflowPasosEjecucion.AsNoTracking()
            .Where(p => p.IdEjecucion == idEjecucion)
            .OrderBy(p => p.IdPasoEjecucion).ToListAsync(ct);

    public async Task AddAsync(WorkflowPasoEjecucion paso, CancellationToken ct = default)
        => await _context.WorkflowPasosEjecucion.AddAsync(paso, ct);

    public async Task UpdateAsync(WorkflowPasoEjecucion paso, CancellationToken ct = default)
    {
        _context.WorkflowPasosEjecucion.Update(paso);
        await Task.CompletedTask;
    }
}

public class ConfiguracionWorkflowRepository : IConfiguracionWorkflowRepository
{
    private readonly AsistenteDbContext _context;
    public ConfiguracionWorkflowRepository(AsistenteDbContext context) => _context = context;

    public async Task<ConfiguracionWorkflow?> GetAsync(CancellationToken ct = default)
        => await _context.ConfiguracionesWorkflow.FirstOrDefaultAsync(c => c.IdConfiguracion == 1, ct)
           ?? new ConfiguracionWorkflow();

    public async Task UpdateAsync(ConfiguracionWorkflow config, CancellationToken ct = default)
    {
        config.FechaActualizacion = System.DateTime.UtcNow;
        _context.ConfiguracionesWorkflow.Update(config);
        await Task.CompletedTask;
    }
}
