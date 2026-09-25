using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Asistente.Infrastructure.Services;

/// <summary>
/// Evalúa los disparadores de eventos configurados 100% desde UI:
/// - Cron: se sincronizan como jobs Quartz (grupo "DisparadoresEvento"); el job
///   <see cref="DisparadorEventoJob"/> dispara el evento.
/// - SondeoBD: cada 60s ejecuta las consultas vencidas y dispara el evento por
///   fila (modo PorFila) o una vez si hay filas (modo SiHayFilas).
/// Los disparadores tipo Documento se evalúan en el detector de procesamiento.
/// Todo es tolerante a fallos por disparador (uno roto no tumba a los demás).
/// </summary>
public class EvaluadorDisparadoresBackgroundService : BackgroundService
{
    private const string GrupoQuartz = "DisparadoresEvento";
    private static readonly TimeSpan IntervaloSondeo = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _serviceProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<EvaluadorDisparadoresBackgroundService> _logger;

    public EvaluadorDisparadoresBackgroundService(
        IServiceProvider serviceProvider,
        ISchedulerFactory schedulerFactory,
        ILogger<EvaluadorDisparadoresBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        if (stoppingToken.IsCancellationRequested) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SincronizarCronAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar disparadores Cron con Quartz.");
            }

            try
            {
                await EvaluarSondeosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al evaluar disparadores de sondeo.");
            }

            try
            {
                await Task.Delay(IntervaloSondeo, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SincronizarCronAsync(CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        await scheduler.Start(ct);

        using var scope = _serviceProvider.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IDisparadorEventoService>();
        var activos = (await servicio.ObtenerActivosAsync(ct))
            .Where(d => d.Tipo == DisparadorEvento.Tipos.Cron)
            .ToList();

        var esperados = new HashSet<string>();
        foreach (var d in activos)
        {
            string? cron = null;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(d.ConfigJson) ? "{}" : d.ConfigJson);
                if (doc.RootElement.TryGetProperty("cron", out var c) && c.ValueKind == JsonValueKind.String)
                    cron = c.GetString();
            }
            catch { /* se registra abajo como error de config */ }

            if (string.IsNullOrWhiteSpace(cron))
            {
                _logger.LogWarning("Disparador {Id} tipo Cron sin expresión válida; se omite.", d.IdDisparador);
                continue;
            }

            var nombre = $"disparador-{d.IdDisparador}";
            esperados.Add(nombre);
            try
            {
                var jobKey = new JobKey(nombre, GrupoQuartz);
                if (await scheduler.CheckExists(jobKey, ct))
                    await scheduler.DeleteJob(jobKey, ct);

                var job = JobBuilder.Create<DisparadorEventoJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData("IdDisparador", d.IdDisparador)
                    .Build();
                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"trigger-{nombre}", GrupoQuartz)
                    .WithCronSchedule(cron)
                    .Build();
                await scheduler.ScheduleJob(job, trigger, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo programar el disparador Cron {Id}.", d.IdDisparador);
            }
        }

        // Eliminar jobs huérfanos (disparadores borrados o desactivados).
        var allJobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(GrupoQuartz), ct);
        foreach (var jobKey in allJobKeys)
        {
            if (!esperados.Contains(jobKey.Name))
            {
                await scheduler.DeleteJob(jobKey, ct);
                _logger.LogInformation("Job Quartz huérfano {Job} eliminado.", jobKey.Name);
            }
        }
    }

    private async Task EvaluarSondeosAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IDisparadorEventoService>();
        var motor = scope.ServiceProvider.GetRequiredService<IEventoMotorService>();
        var eventos = scope.ServiceProvider.GetRequiredService<IEventoEmpresarialService>();
        var conexiones = scope.ServiceProvider.GetRequiredService<IConexionBaseDatosRepository>();
        var cifrador = scope.ServiceProvider.GetRequiredService<IConexionCifrador>();
        var executor = scope.ServiceProvider.GetRequiredService<ISqlQueryExecutor>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var ahora = DateTime.UtcNow;
        var sondeos = (await servicio.ObtenerActivosAsync(ct))
            .Where(d => d.Tipo == DisparadorEvento.Tipos.SondeoBD)
            .ToList();

        foreach (var d in sondeos)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                using var cfg = JsonDocument.Parse(string.IsNullOrWhiteSpace(d.ConfigJson) ? "{}" : d.ConfigJson);
                var root = cfg.RootElement;
                var intervalo = root.TryGetProperty("intervaloSegundos", out var iv) && iv.ValueKind == JsonValueKind.Number
                    ? iv.GetInt32() : 300;
                if (d.UltimaEjecucion.HasValue && (ahora - d.UltimaEjecucion.Value).TotalSeconds < intervalo)
                    continue; // aún no vence

                var idConexion = root.GetProperty("idConexion").GetInt32();
                var sql = root.GetProperty("consultaSql").GetString() ?? "";
                var modo = root.TryGetProperty("modo", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString()! : "PorFila";
                var maxFilas = root.TryGetProperty("maxFilas", out var mf) && mf.ValueKind == JsonValueKind.Number
                    ? Math.Clamp(mf.GetInt32(), 1, 500) : 50;
                var contextoFijo = root.TryGetProperty("contextoFijo", out var cf) && cf.ValueKind == JsonValueKind.Object
                    ? cf.GetRawText() : null;

                var conexion = await conexiones.GetByIdAsync(idConexion);
                if (conexion == null || !conexion.Activa)
                {
                    _logger.LogWarning("Sondeo {Id}: conexión {Con} inexistente o inactiva; se omite.", d.IdDisparador, idConexion);
                    await servicio.MarcarEjecucionAsync(d.IdDisparador, null, ct);
                    continue;
                }
                if (!EsSelectSeguro(sql))
                {
                    _logger.LogWarning("Sondeo {Id}: la consulta dejó de ser SELECT válido; se omite.", d.IdDisparador);
                    await servicio.MarcarEjecucionAsync(d.IdDisparador, null, ct);
                    continue;
                }

                var cadena = cifrador.Descifrar(conexion.CadenaConexionCifrada);
                var filas = (await executor.ExecuteReadOnlyAsync(cadena, sql, null, maxFilas, ct)).ToList();

                var evento = await eventos.ObtenerPorIdAsync(d.IdEvento, ct);
                if (evento == null || !evento.Activo)
                {
                    _logger.LogInformation("Sondeo {Id}: evento inexistente o inactivo; se omite.", d.IdDisparador);
                    await servicio.MarcarEjecucionAsync(d.IdDisparador, null, ct);
                    continue;
                }

                if (modo.Equals("SiHayFilas", StringComparison.OrdinalIgnoreCase))
                {
                    if (filas.Count > 0)
                        await motor.DispararEventoAsync(evento.Codigo,
                            CombinarContexto(contextoFijo, new Dictionary<string, object?> { ["filas"] = filas.Count }),
                            null, ct);
                }
                else // PorFila
                {
                    foreach (var fila in filas)
                    {
                        if (ct.IsCancellationRequested) break;
                        await motor.DispararEventoAsync(evento.Codigo, FilaAJson(fila), null, ct);
                    }
                }

                await servicio.MarcarEjecucionAsync(d.IdDisparador, null, ct);
                _logger.LogInformation("Sondeo {Id} evaluado: {N} fila(s).", d.IdDisparador, filas.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al evaluar sondeo {Id}.", d.IdDisparador);
                try { await servicio.MarcarEjecucionAsync(d.IdDisparador, null, ct); } catch { /* no bloquear el ciclo */ }
            }
        }
    }

    private static bool EsSelectSeguro(string sql)
    {
        var t = (sql ?? "").TrimStart();
        if (!t.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) return false;
        var cuerpo = t.TrimEnd();
        if (cuerpo.EndsWith(";")) cuerpo = cuerpo[..^1];
        if (cuerpo.Contains(';')) return false;
        return true;
    }

    private static string CombinarContexto(string? fijoJson, Dictionary<string, object?> extras)
    {
        try
        {
            var dict = string.IsNullOrWhiteSpace(fijoJson)
                ? new Dictionary<string, object?>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(fijoJson)
                  ?? new Dictionary<string, object?>();
            foreach (var kv in extras) dict[kv.Key] = kv.Value;
            return System.Text.Json.JsonSerializer.Serialize(dict);
        }
        catch { return System.Text.Json.JsonSerializer.Serialize(extras); }
    }

    private static string FilaAJson(Dictionary<string, object?> fila)
    {
        try { return System.Text.Json.JsonSerializer.Serialize(fila); }
        catch { return "{}"; }
    }
}
