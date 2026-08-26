using System;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Asistente.Infrastructure.Services;

/// <summary>
/// BackgroundService que registra en Quartz.NET todas las TareasProgramadas activas al iniciar
/// la aplicación (ETAPA 13 - Actividad 5). Cada tarea se dispara según su expresión Cron y ejecuta
/// el workflow asociado de forma desacoplada.
/// </summary>
public class ProgramadorTareasBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<ProgramadorTareasBackgroundService> _logger;

    public ProgramadorTareasBackgroundService(
        IServiceProvider serviceProvider,
        ISchedulerFactory schedulerFactory,
        ILogger<ProgramadorTareasBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Esperar brevemente a que el proveedor y la base de datos estén listos (semilla/migraciones).
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        try
        {
            await ProgramarTareasAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al programar las tareas programadas con Quartz.NET.");
        }
    }

    /// <summary>
    /// Programa (o reprograma) todas las tareas activas. Puede llamarse tras crear/editar tareas.
    /// </summary>
    public async Task ProgramarTareasAsync(CancellationToken ct = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        await scheduler.Start(ct);

        using var scope = _serviceProvider.CreateScope();
        var tareaRepo = scope.ServiceProvider.GetRequiredService<ITareaProgramadaRepository>();
        var tareas = await tareaRepo.GetActivasAsync(ct);

        foreach (var tarea in tareas)
        {
            try
            {
                var jobKey = new JobKey($"tarea-{tarea.IdTarea}", "TareasProgramadas");
                if (await scheduler.CheckExists(jobKey, ct))
                    await scheduler.DeleteJob(jobKey, ct);

                var job = JobBuilder.Create<TareaProgramadaJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData("IdTarea", tarea.IdTarea)
                    .UsingJobData("IdWorkflow", tarea.IdWorkflow)
                    .UsingJobData("IdUsuario", tarea.UsuarioCreacion)
                    .UsingJobData("NombreTarea", tarea.Nombre)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"trigger-tarea-{tarea.IdTarea}", "TareasProgramadas")
                    .WithCronSchedule(tarea.ExpresionCron)
                    .Build();

                await scheduler.ScheduleJob(job, trigger, ct);
                _logger.LogInformation("Quartz: tarea '{T}' programada con cron '{C}'.", tarea.Nombre, tarea.ExpresionCron);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quartz: no se pudo programar la tarea {Id}.", tarea.IdTarea);
            }
        }
    }
}
