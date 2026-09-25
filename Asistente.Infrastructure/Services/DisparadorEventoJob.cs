using System;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Asistente.Infrastructure.Services;

/// <summary>
/// Job de Quartz.NET que dispara un evento desde un disparador tipo Cron.
/// Sigue el mismo patrón que <see cref="TareaProgramadaJob"/>.
/// </summary>
public class DisparadorEventoJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DisparadorEventoJob> _logger;

    public DisparadorEventoJob(IServiceProvider serviceProvider, ILogger<DisparadorEventoJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var idDisparador = context.JobDetail.JobDataMap.GetIntValue("IdDisparador");

        using var scope = _serviceProvider.CreateScope();
        var disparadores = scope.ServiceProvider.GetRequiredService<IDisparadorEventoService>();
        var motor = scope.ServiceProvider.GetRequiredService<IEventoMotorService>();

        try
        {
            var d = await disparadores.ObtenerPorIdAsync(idDisparador, context.CancellationToken);
            if (d == null || !d.Activo)
            {
                _logger.LogInformation("Quartz: disparador {Id} ya no existe o está inactivo; se omite.", idDisparador);
                return;
            }

            var contexto = ExtraerContexto(d.ConfigJson);
            var procesado = await motor.DispararEventoAsync(
                (await scope.ServiceProvider.GetRequiredService<IEventoEmpresarialService>()
                    .ObtenerPorIdAsync(d.IdEvento, context.CancellationToken))?.Codigo
                    ?? throw new InvalidOperationException($"Evento {d.IdEvento} no encontrado."),
                contexto,
                idUsuario: null,
                context.CancellationToken);

            await disparadores.MarcarEjecucionAsync(idDisparador, context.NextFireTimeUtc?.UtcDateTime, context.CancellationToken);
            _logger.LogInformation("Quartz: disparador {Id} disparó evento procesado {P}.", idDisparador, procesado.IdEventoProcesado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quartz: error al ejecutar el disparador {Id}.", idDisparador);
        }
    }

    private static string? ExtraerContexto(string configJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            if (doc.RootElement.TryGetProperty("contexto", out var ctx)
                && ctx.ValueKind != System.Text.Json.JsonValueKind.Null
                && ctx.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                return ctx.GetRawText();
        }
        catch { /* sin contexto: se dispara sin datos */ }
        return null;
    }
}
