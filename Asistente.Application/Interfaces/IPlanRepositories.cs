using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Application.Interfaces;

public interface IPlanRepository
{
    Task<Plan> AddAsync(Plan plan, CancellationToken cancellationToken = default);
    Task UpdateAsync(Plan plan, CancellationToken cancellationToken = default);
    Task<Plan?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Plan>> GetRecentAsync(int cantidad = 50, CancellationToken cancellationToken = default);
    Task<List<Plan>> GetByEstadoAsync(string estado, CancellationToken cancellationToken = default);
}

public interface IPlanStepRepository
{
    Task<PlanStep> AddAsync(PlanStep step, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlanStep step, CancellationToken cancellationToken = default);
    Task<List<PlanStep>> GetByPlanAsync(int idPlan, CancellationToken cancellationToken = default, bool asNoTracking = false);
    Task<PlanStep?> GetByIdAsync(int idStep, CancellationToken cancellationToken = default);
}

public interface IPlanDependencyRepository
{
    Task<PlanDependency> AddAsync(PlanDependency dependency, CancellationToken cancellationToken = default);
    Task<List<PlanDependency>> GetByPlanAsync(int idPlan, CancellationToken cancellationToken = default);
}

public interface IPlanExecutionLogRepository
{
    Task<PlanExecutionLog> AddAsync(PlanExecutionLog log, CancellationToken cancellationToken = default);
    Task<List<PlanExecutionLog>> GetByPlanAsync(int idPlan, CancellationToken cancellationToken = default);
}
