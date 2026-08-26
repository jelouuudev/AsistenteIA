using System;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asistente.Infrastructure.Services;

public class ProcesamientoDocumentalBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcesamientoDocumentalBackgroundService> _logger;
    private readonly ProcesamientoConfig _config;

    public ProcesamientoDocumentalBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ProcesamientoDocumentalBackgroundService> logger,
        IOptions<ProcesamientoConfig> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Servicio de procesamiento documental iniciado. Frecuencia: {Frecuencia}s, Max docs por ciclo: {Max}",
            _config.FrecuenciaSegundos, _config.MaxDocumentosPorCiclo);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarDocumentosPendientesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo del servicio de procesamiento documental.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.FrecuenciaSegundos), stoppingToken);
        }

        _logger.LogInformation("Servicio de procesamiento documental detenido.");
    }

    private async Task ProcesarDocumentosPendientesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var procesamientoService = scope.ServiceProvider.GetRequiredService<IProcesamientoDocumentalService>();

        try
        {
            // Obtener documentos pendientes
            var pendientes = (await procesamientoService.ObtenerTodosAsync())
                .Where(p => p.Estado == EstadoProcesamiento.Pendiente.ToString())
                .Take(_config.MaxDocumentosPorCiclo)
                .ToList();

            if (!pendientes.Any())
            {
                return;
            }

            _logger.LogInformation("Procesando {Count} documentos pendientes...", pendientes.Count);

            foreach (var documento in pendientes)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    _logger.LogInformation(
                        "Procesando documento '{Nombre}' (versión {Version})...",
                        documento.DocumentoNombre, documento.NumeroVersion);

                    await procesamientoService.ProcesarDocumentoAsync(documento.IdVersionDocumento);

                    _logger.LogInformation(
                        "Documento '{Nombre}' procesado exitosamente. {Chunks} chunks, {Caracteres} caracteres.",
                        documento.DocumentoNombre, documento.TotalChunks, documento.TotalCaracteres);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error al procesar documento '{Nombre}' (versión {Version})",
                        documento.DocumentoNombre, documento.NumeroVersion);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener documentos pendientes.");
        }
    }
}
