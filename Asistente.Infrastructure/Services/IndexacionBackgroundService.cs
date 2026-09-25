using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asistente.Infrastructure.Services;

public class IndexacionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IndexacionBackgroundService> _logger;
    private readonly Dictionary<int, int> _intentosFallidos = new();
    private const int MaxIntentosSinChunks = 3;

    public IndexacionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<IndexacionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de indexación automática iniciado.");

        bool primeraEjecucion = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IndexarDocumentosProcesadosAsync(stoppingToken, primeraEjecucion);
                primeraEjecucion = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo del servicio de indexación.");
            }

            // El Task.Delay debe ir en su propio try: si se cancela durante el apagado,
            // su TaskCanceledException NO debe escapar de ExecuteAsync (lo que, con
            // BackgroundServiceExceptionBehavior = StopHost, tumbaría toda la API).
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Servicio de indexación automática detenido.");
    }

    private async Task IndexarDocumentosProcesadosAsync(CancellationToken cancellationToken, bool forzarReindexacion = false)
    {
        using var scope = _serviceProvider.CreateScope();
        var indexacionService = scope.ServiceProvider.GetRequiredService<IIndexacionService>();
        var procesamientoService = scope.ServiceProvider.GetRequiredService<IProcesamientoDocumentalService>();

        try
        {
            if (forzarReindexacion)
            {
                _logger.LogInformation("Primera ejecución: reparando documentos con chunks faltantes...");
                var reparados = await procesamientoService.RepararDocumentosConChunksFaltantesAsync();
                if (reparados > 0)
                {
                    _logger.LogInformation("Reparados {Count} documentos con chunks faltantes.", reparados);
                }
            }

            var todosIndexados = (await indexacionService.ObtenerTodosAsync()).ToList();
            var idsIndexados = todosIndexados.Select(i => i.IdDocumentoProcesado).ToHashSet();

            // Re-indexar documentos indexados previos (solo en primera ejecución, para recargar memoria)
            if (forzarReindexacion)
            {
                var indexados = todosIndexados
                    .Where(i => i.Estado == EstadoIndexacion.Indexado.ToString())
                    .ToList();

                if (indexados.Any())
                {
                    _logger.LogInformation("Primera ejecución: re-indexando {Count} documentos ya indexados para recargar memoria.", indexados.Count);

                    foreach (var doc in indexados)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        try
                        {
                            await indexacionService.ReindexarDocumentoAsync(doc.IdDocumentoProcesado);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al re-indexar documento '{Nombre}' (procesado {Id}) en carga inicial.", doc.DocumentoNombre, doc.IdDocumentoProcesado);
                        }
                    }
                }
            }

            // Detectar documentos procesados que NO tienen registro de indexación
            var todosProcesados = (await procesamientoService.ObtenerTodosAsync()).ToList();
            var procesadosSinIndexar = todosProcesados
                .Where(p => p.Estado == "Procesado" && !idsIndexados.Contains(p.IdDocumentoProcesado))
                .ToList();

            if (procesadosSinIndexar.Any())
            {
                _logger.LogInformation("Detectados {Count} documentos procesados sin indexar. Indexando...", procesadosSinIndexar.Count);

                foreach (var doc in procesadosSinIndexar)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try
                    {
                        _logger.LogInformation(
                            "Indexando documento sin registro '{Nombre}' (procesado {Id})...",
                            doc.DocumentoNombre, doc.IdDocumentoProcesado);

                        await indexacionService.IndexarDocumentoAsync(doc.IdDocumentoProcesado);

                        _logger.LogInformation(
                            "Documento '{Nombre}' indexado exitosamente.",
                            doc.DocumentoNombre);

                        // El documento ya tiene vectores en la base vectorial: recién ahora
                        // una búsqueda RAG puede encontrarlo. Se avisa con DOC_INDEXADO para
                        // automatizaciones (ej. resumir el documento al subirlo). Lo hace el
                        // workflow atado a ese evento, no DOC_PROCESADO (que se dispara antes
                        // de indexar y siempre encontraba vacío).
                        await DispararDocIndexadoAsync(scope, doc.IdDocumentoProcesado, false, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error al indexar documento sin registro '{Nombre}' (procesado {Id})",
                            doc.DocumentoNombre, doc.IdDocumentoProcesado);
                    }
                }
            }

            // Indexar documentos con estado Pendiente, Error o EnProceso (atascados).
            // "EnProceso" puede quedarse pegado si la indexación anterior falló a mitad.
            var pendientes = todosIndexados
                .Where(i => i.Estado == EstadoIndexacion.Pendiente.ToString() ||
                           i.Estado == EstadoIndexacion.Error.ToString() ||
                           i.Estado == EstadoIndexacion.EnProceso.ToString())
                .ToList();

            if (!pendientes.Any()) return;

            _logger.LogInformation("Indexando {Count} documentos pendientes...", pendientes.Count);

            foreach (var documento in pendientes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Skip documents that have exceeded max retries (e.g., 0 chunks repeatedly)
                if (_intentosFallidos.TryGetValue(documento.IdDocumentoProcesado, out var intentos) &&
                    intentos >= MaxIntentosSinChunks)
                {
                    continue;
                }

                try
                {
                    _logger.LogInformation(
                        "Indexando documento '{Nombre}' (procesado {Id})...",
                        documento.DocumentoNombre, documento.IdDocumentoProcesado);

                    var antes = documento.TotalEmbeddings;
                    await indexacionService.IndexarDocumentoAsync(documento.IdDocumentoProcesado);

                    // Re-fetch to get the actual state after indexing
                    var despues = (await indexacionService.ObtenerTodosAsync())
                        .FirstOrDefault(i => i.IdDocumentoProcesado == documento.IdDocumentoProcesado);

                    if (despues != null && despues.TotalEmbeddings == antes && despues.Estado == EstadoIndexacion.Error.ToString())
                    {
                        _intentosFallidos[documento.IdDocumentoProcesado] = intentos + 1;
                        _logger.LogWarning(
                            "Documento '{Nombre}' (procesado {Id}) no generó embeddings tras intento {Intento}/{Max}. Posiblemente 0 chunks.",
                            documento.DocumentoNombre, documento.IdDocumentoProcesado, intentos + 1, MaxIntentosSinChunks);
                    }
                    else
                    {
                        _intentosFallidos.Remove(documento.IdDocumentoProcesado);
                        _logger.LogInformation(
                            "Documento '{Nombre}' indexado exitosamente.",
                            documento.DocumentoNombre);

                        // Solo avisar si es una indexación nueva (venía de Pendiente/Error/
                        // EnProceso). Los ya indexados no re-disparan en cada ciclo.
                        var eraIndexado = string.Equals(documento.Estado,
                            EstadoIndexacion.Indexado.ToString(), StringComparison.OrdinalIgnoreCase);
                        await DispararDocIndexadoAsync(scope, documento.IdDocumentoProcesado, eraIndexado, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _intentosFallidos[documento.IdDocumentoProcesado] = (_intentosFallidos.GetValueOrDefault(documento.IdDocumentoProcesado) + 1);
                    _logger.LogError(ex,
                        "Error al indexar documento '{Nombre}' (procesado {Id})",
                        documento.DocumentoNombre, documento.IdDocumentoProcesado);
                }
            }
        }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener documentos para indexar.");
            }
        }

    /// <summary>
    /// Dispara DOC_INDEXADO cuando un documento queda con vectores por primera vez.
    /// Nunca rompe la indexación: si el evento no existe o falla, solo se registra.
    /// El contexto lleva Nombre/Codigo para que los pasos usen {{Nombre}}.
    /// </summary>
    private async Task DispararDocIndexadoAsync(IServiceScope scope, int idDocumentoProcesado, bool yaEstabaIndexado, CancellationToken ct)
    {
        if (yaEstabaIndexado) return;
        try
        {
            var indexacionService = scope.ServiceProvider.GetRequiredService<IIndexacionService>();
            var actual = (await indexacionService.ObtenerTodosAsync())
                .FirstOrDefault(i => i.IdDocumentoProcesado == idDocumentoProcesado);
            if (actual == null || !string.Equals(actual.Estado,
                    EstadoIndexacion.Indexado.ToString(), StringComparison.OrdinalIgnoreCase))
                return;

            // Verificar si hay un disparador tipo Documento activo para DOC_INDEXADO
            var disparadoresService = scope.ServiceProvider.GetRequiredService<IDisparadorEventoService>();
            var activos = await disparadoresService.ObtenerActivosAsync(ct);
            var evento = activos.FirstOrDefault(d => d.CodigoEvento == "DOC_INDEXADO" && d.Tipo == "Documento");
            if (evento == null)
            {
                _logger.LogInformation("No hay disparador activo para DOC_INDEXADO; se omite evento para documento '{Nombre}'.", actual.DocumentoNombre);
                return;
            }

            var contexto = JsonSerializer.Serialize(new
            {
                IdDocumento = actual.IdDocumento,
                Codigo = actual.DocumentoCodigo,
                Nombre = actual.DocumentoNombre,
                IdDocumentoProcesado = actual.IdDocumentoProcesado,
                TotalChunks = actual.TotalChunks
            });

            var motor = scope.ServiceProvider.GetRequiredService<IEventoMotorService>();
            await motor.DispararEventoAsync("DOC_INDEXADO", contexto, null, ct);
            _logger.LogInformation("Evento DOC_INDEXADO disparado para documento '{Nombre}'.",
                actual.DocumentoNombre);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo disparar DOC_INDEXADO para procesado {Id}.", idDocumentoProcesado);
        }
    }
}
