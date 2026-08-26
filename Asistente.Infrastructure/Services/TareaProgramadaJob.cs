using System;
using System.Threading;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Asistente.Infrastructure.Services;

/// <summary>
/// Job de Quartz.NET que ejecuta un workflow asociado a una TareaProgramada (ETAPA 13 - Actividad 5).
/// Se invoca según la expresión Cron configurada. La ejecución es desacoplada y se registra en auditoría.
/// El IServiceProvider se resuelve por inyección de dependencias (no por JobDataMap).
/// </summary>
public class TareaProgramadaJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TareaProgramadaJob> _logger;

    public TareaProgramadaJob(IServiceProvider serviceProvider, ILogger<TareaProgramadaJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var idWorkflow = context.JobDetail.JobDataMap.GetIntValue("IdWorkflow");
        var idUsuario = context.JobDetail.JobDataMap.GetIntValue("IdUsuario");
        var idTarea = context.JobDetail.JobDataMap.GetIntValue("IdTarea");
        var nombreTarea = context.JobDetail.JobDataMap.GetString("NombreTarea") ?? $"Tarea {idWorkflow}";

        _logger.LogInformation("Quartz: ejecutando tarea programada '{Tarea}' (workflow {W}).", nombreTarea, idWorkflow);

        // Crear un scope propio para resolver servicios scoped (WorkflowEngine es scoped).
        using var scope = _serviceProvider.CreateScope();
        var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        var tareaRepo = scope.ServiceProvider.GetRequiredService<ITareaProgramadaRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<Asistente.Domain.Interfaces.IUnitOfWork>();

        try
        {
            // confirmado: true para que los pasos sensibles se ejecuten en modo desatendido.
            var resultado = await workflowEngine.EjecutarAsync(idWorkflow, idUsuario, null, confirmado: true, cancellationToken: context.CancellationToken);

            var tarea = await tareaRepo.GetByIdAsync(idTarea);
            if (tarea != null)
            {
                tarea.UltimaEjecucion = DateTime.UtcNow;
                tarea.ProximaEjecucion = context.NextFireTimeUtc?.UtcDateTime;
                await tareaRepo.UpdateAsync(tarea);
                await uow.SaveChangesAsync(context.CancellationToken);
            }

            _logger.LogInformation("Quartz: tarea '{Tarea}' finalizó con estado {Estado}.", nombreTarea, resultado.Estado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quartz: error al ejecutar la tarea programada '{Tarea}'.", nombreTarea);
        }
    }
}
