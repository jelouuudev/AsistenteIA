using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IWorkflowRepository
{
    Task<Workflow?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Workflow?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<IEnumerable<Workflow>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Workflow>> GetActivosAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task UpdateAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task DeleteAsync(Workflow workflow, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default);
}

public interface IWorkflowPasoRepository
{
    Task<WorkflowPaso?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowPaso>> GetByWorkflowAsync(int idWorkflow, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowPaso paso, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowPaso paso, CancellationToken cancellationToken = default);
    Task DeleteByWorkflowAsync(int idWorkflow, CancellationToken cancellationToken = default);
    /// <summary>
    /// Pasos con seguimiento de cambios (para reemplazo atómico dentro de un SaveChanges).
    /// </summary>
    Task<List<WorkflowPaso>> GetTrackedByWorkflowAsync(int idWorkflow, CancellationToken cancellationToken = default);
    /// <summary>Marca pasos para borrado (se confirma con SaveChanges, atómico).</summary>
    void RemoveRange(IEnumerable<WorkflowPaso> pasos);
}

public interface IWorkflowEjecucionRepository
{
    Task<WorkflowEjecucion?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowEjecucion>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkflowEjecucion>> GetByWorkflowAsync(int idWorkflow, CancellationToken cancellationToken = default);
    Task<WorkflowEjecucion?> GetPendienteConfirmacionAsync(int idUsuario, CancellationToken cancellationToken = default);
    Task<WorkflowEjecucion?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowEjecucion ejecucion, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowEjecucion ejecucion, CancellationToken cancellationToken = default);
}

public interface IWorkflowPasoEjecucionRepository
{
    Task<IEnumerable<WorkflowPasoEjecucion>> GetByEjecucionAsync(int idEjecucion, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowPasoEjecucion paso, CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkflowPasoEjecucion paso, CancellationToken cancellationToken = default);
}

public interface IConfiguracionWorkflowRepository
{
    Task<ConfiguracionWorkflow?> GetAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(ConfiguracionWorkflow config, CancellationToken cancellationToken = default);
}
