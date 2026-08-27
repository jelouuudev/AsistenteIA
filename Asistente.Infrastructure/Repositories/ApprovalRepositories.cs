using Asistente.Application.Interfaces;
using Asistente.Domain.Entities.Aprobaciones;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ApprovalRequestRepository : IApprovalRequestRepository
{
    private readonly AsistenteDbContext _context;
    public ApprovalRequestRepository(AsistenteDbContext context) => _context = context;

    public async Task<ApprovalRequest> AddAsync(ApprovalRequest entity, CancellationToken ct = default)
    {
        await _context.ApprovalRequests.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(ApprovalRequest entity, CancellationToken ct = default)
    {
        _context.ApprovalRequests.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ApprovalRequest?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.ApprovalRequests
            .Include(a => a.Asignados)
            .Include(a => a.Decisiones)
            .Include(a => a.Policy)
            .FirstOrDefaultAsync(a => a.IdApproval == id, ct);

    public async Task<ApprovalRequest?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        => await _context.ApprovalRequests
            .Include(a => a.Asignados)
            .Include(a => a.Decisiones)
            .FirstOrDefaultAsync(a => a.Codigo == codigo, ct);

    public async Task<List<ApprovalRequest>> GetAllAsync(CancellationToken ct = default)
        => await _context.ApprovalRequests
            .Include(a => a.Asignados)
            .Include(a => a.Decisiones)
            .OrderByDescending(a => a.FechaSolicitud)
            .ToListAsync(ct);

    public async Task<List<ApprovalRequest>> GetByEstadoAsync(EstadoAprobacion estado, CancellationToken ct = default)
        => await _context.ApprovalRequests
            .Where(a => a.Estado == estado)
            .OrderByDescending(a => a.FechaSolicitud)
            .ToListAsync(ct);

    public async Task<List<ApprovalRequest>> GetPendientesParaAsync(int idUsuario, CancellationToken ct = default)
        => await _context.ApprovalRequests
            .Include(a => a.Asignados)
            .Where(a => a.Estado == EstadoAprobacion.Pendiente || a.Estado == EstadoAprobacion.EnRevision || a.Estado == EstadoAprobacion.Delegado)
            .Where(a => a.Asignados.Any(x => x.IdUsuario == idUsuario && x.Estado == "Pendiente"))
            .OrderByDescending(a => a.FechaSolicitud)
            .ToListAsync(ct);
}

public class ApprovalDecisionRepository : IApprovalDecisionRepository
{
    private readonly AsistenteDbContext _context;
    public ApprovalDecisionRepository(AsistenteDbContext context) => _context = context;

    public async Task<ApprovalDecision> AddAsync(ApprovalDecision entity, CancellationToken ct = default)
    {
        await _context.ApprovalDecisions.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<ApprovalDecision>> GetByApprovalAsync(int idApproval, CancellationToken ct = default)
        => await _context.ApprovalDecisions
            .Where(d => d.IdApproval == idApproval)
            .OrderBy(d => d.FechaDecision)
            .ToListAsync(ct);
}

public class ApprovalAssigneeRepository : IApprovalAssigneeRepository
{
    private readonly AsistenteDbContext _context;
    public ApprovalAssigneeRepository(AsistenteDbContext context) => _context = context;

    public async Task<ApprovalAssignee> AddAsync(ApprovalAssignee entity, CancellationToken ct = default)
    {
        await _context.ApprovalAssignees.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(ApprovalAssignee entity, CancellationToken ct = default)
    {
        _context.ApprovalAssignees.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ApprovalAssignee>> GetByApprovalAsync(int idApproval, CancellationToken ct = default)
        => await _context.ApprovalAssignees
            .Where(a => a.IdApproval == idApproval)
            .ToListAsync(ct);
}

public class ApprovalPolicyRepository : IApprovalPolicyRepository
{
    private readonly AsistenteDbContext _context;
    public ApprovalPolicyRepository(AsistenteDbContext context) => _context = context;

    public async Task<ApprovalPolicy> AddAsync(ApprovalPolicy entity, CancellationToken ct = default)
    {
        await _context.ApprovalPolicies.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(ApprovalPolicy entity, CancellationToken ct = default)
    {
        _context.ApprovalPolicies.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ApprovalPolicy>> GetAllAsync(CancellationToken ct = default)
        => await _context.ApprovalPolicies.ToListAsync(ct);

    public async Task<ApprovalPolicy?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.ApprovalPolicies.FirstOrDefaultAsync(p => p.IdPolicy == id, ct);

    public async Task<ApprovalPolicy?> GetActivaAsync(CancellationToken ct = default)
        => await _context.ApprovalPolicies.FirstOrDefaultAsync(p => p.Activo, ct);
}
