using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asistente.Infrastructure.Services;

/// <summary>
/// Procesador desacoplado de eventos empresariales (ETAPA 13 - Actividad 4).
///
/// De forma periódica consulta los EventosProcesados en estado Pendiente / EnProceso / Reintentando
/// y los procesa vía IEventoMotorService.ProcesarEventoAsync, respetando la concurrencia máxima
/// configurada y aplicando la política de reintentos del motor. Esto desacopla la detección del
/// evento de su ejecución, garantizando alta disponibilidad y procesamiento asíncrono.
/// </summary>
public class ProcesadorEventosBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcesadorEventosBackgroundService> _logger;

    public ProcesadorEventosBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ProcesadorEventosBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de procesamiento de eventos iniciado.");

        // Esperar a que la base de datos y la semilla estén listas.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int frecuenciaMs = 3000;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var eventoProcesadoRepo = scope.ServiceProvider.GetRequiredService<IEventoProcesadoRepository>();
                var configRepo = scope.ServiceProvider.GetRequiredService<IConfiguracionEventoMotorRepository>();
                var motor = scope.ServiceProvider.GetRequiredService<IEventoMotorService>();

                var config = await configRepo.GetAsync(stoppingToken);
                frecuenciaMs = Math.Max(config.FrecuenciaProcesadorMs, 1000);
                var maxConcurrencia = Math.Max(config.EventosSimultaneosMax, 1);

                var pendientes = (await eventoProcesadoRepo.GetAllAsync(stoppingToken))
                    .Where(e => e.Estado is "Pendiente" or "EnProceso" or "Reintentando")
                    .OrderBy(e => e.FechaHora)
                    .Take(maxConcurrencia)
                    .ToList();

                if (pendientes.Count > 0)
                {
                    var tareas = new List<Task>();
                    foreach (var p in pendientes)
                    {
                        tareas.Add(ProcesarUnoAsync(motor, p.IdEventoProcesado, stoppingToken));
                    }
                    await Task.WhenAll(tareas);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo del procesador de eventos.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(frecuenciaMs), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Servicio de procesamiento de eventos detenido.");
    }

    private async Task ProcesarUnoAsync(IEventoMotorService motor, int idEventoProcesado, CancellationToken ct)
    {
        try
        {
            await motor.ProcesarEventoAsync(idEventoProcesado, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar el evento {Id}.", idEventoProcesado);
        }
    }
}
