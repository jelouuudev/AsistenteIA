using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Application.Interfaces;

public interface IAgentExecutionRepository
{
    Task<AgentExecution> AddAsync(AgentExecution execution, CancellationToken cancellationToken = default);
    Task UpdateAsync(AgentExecution execution, CancellationToken cancellationToken = default);
    Task<AgentExecution?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<AgentExecution>> GetRecentAsync(int cantidad = 50, CancellationToken cancellationToken = default);
}

public interface IAgentExecutionStepRepository
{
    Task<AgentExecutionStep> AddAsync(AgentExecutionStep step, CancellationToken cancellationToken = default);
    Task UpdateAsync(AgentExecutionStep step, CancellationToken cancellationToken = default);
    Task<List<AgentExecutionStep>> GetByExecutionAsync(int idExecution, CancellationToken cancellationToken = default);
}

public interface IAgentExecutionTraceRepository
{
    Task<AgentExecutionTrace> AddAsync(AgentExecutionTrace trace, CancellationToken cancellationToken = default);
    Task<List<AgentExecutionTrace>> GetByExecutionAsync(int idExecution, CancellationToken cancellationToken = default);
}

public interface IAgentCollaborationRuleRepository
{
    Task<List<AgentCollaborationRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<AgentCollaborationRule>> GetActivasAsync(CancellationToken cancellationToken = default);
    Task<AgentCollaborationRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AgentCollaborationRule> AddAsync(AgentCollaborationRule rule, CancellationToken cancellationToken = default);
    Task UpdateAsync(AgentCollaborationRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>Devuelve si origen→destino está permitido según reglas activas (RF Regla 3).</summary>
    Task<bool> EstaPermitidoAsync(int origen, int destino, CancellationToken cancellationToken = default);
}
