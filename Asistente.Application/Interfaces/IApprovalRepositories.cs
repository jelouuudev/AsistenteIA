using Asistente.Domain.Entities.Aprobaciones;

namespace Asistente.Application.Interfaces;

public interface IApprovalRequestRepository
{
    Task<ApprovalRequest> AddAsync(ApprovalRequest entity, CancellationToken ct = default);
    Task UpdateAsync(ApprovalRequest entity, CancellationToken ct = default);
    Task<ApprovalRequest?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApprovalRequest?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
    Task<List<ApprovalRequest>> GetAllAsync(CancellationToken ct = default);
    Task<List<ApprovalRequest>> GetByEstadoAsync(EstadoAprobacion estado, CancellationToken ct = default);
    Task<List<ApprovalRequest>> GetPendientesParaAsync(int idUsuario, CancellationToken ct = default);
}

public interface IApprovalDecisionRepository
{
    Task<ApprovalDecision> AddAsync(ApprovalDecision entity, CancellationToken ct = default);
    Task<List<ApprovalDecision>> GetByApprovalAsync(int idApproval, CancellationToken ct = default);
}

public interface IApprovalAssigneeRepository
{
    Task<ApprovalAssignee> AddAsync(ApprovalAssignee entity, CancellationToken ct = default);
    Task UpdateAsync(ApprovalAssignee entity, CancellationToken ct = default);
    Task<List<ApprovalAssignee>> GetByApprovalAsync(int idApproval, CancellationToken ct = default);
}

public interface IApprovalPolicyRepository
{
    Task<ApprovalPolicy> AddAsync(ApprovalPolicy entity, CancellationToken ct = default);
    Task UpdateAsync(ApprovalPolicy entity, CancellationToken ct = default);
    Task<List<ApprovalPolicy>> GetAllAsync(CancellationToken ct = default);
    Task<ApprovalPolicy?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApprovalPolicy?> GetActivaAsync(CancellationToken ct = default);
}
