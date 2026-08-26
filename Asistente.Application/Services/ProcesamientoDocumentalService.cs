using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asistente.Application.Services;

public class ProcesamientoDocumentalService : IProcesamientoDocumentalService
{
    private readonly IProcesamientoDocumentalRepository _procesamientoRepository;
    private readonly IDocumentoVersionRepository _versionRepository;
    private readonly IDocumentoRepository _documentoRepository;
    private readonly IExtractoraTextoService _extractora;
    private readonly INormalizadorTextoService _normalizador;
    private readonly IChunkingService _chunkingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcesamientoDocumentalService> _logger;
    private readonly ProcesamientoConfig _config;

    public ProcesamientoDocumentalService(
        IProcesamientoDocumentalRepository procesamientoRepository,
        IDocumentoVersionRepository versionRepository,
        IDocumentoRepository documentoRepository,
        IExtractoraTextoService extractora,
        INormalizadorTextoService normalizador,
        IChunkingService chunkingService,
        IUnitOfWork unitOfWork,
        ILogger<ProcesamientoDocumentalService> logger,
        IOptions<ProcesamientoConfig> config)
    {
        _procesamientoRepository = procesamientoRepository;
        _versionRepository = versionRepository;
        _documentoRepository = documentoRepository;
        _extractora = extractora;
        _normalizador = normalizador;
        _chunkingService = chunkingService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<DocumentoProcesadoDto?> ObtenerPorIdAsync(int id)
    {
        var procesado = await _procesamientoRepository.GetByIdWithChunksAsync(id);
        if (procesado == null) return null;
        return MapToDto(procesado);
    }

    public async Task<IEnumerable<DocumentoProcesadoDto>> ObtenerTodosAsync()
    {
        var procesados = await _procesamientoRepository.GetAllAsync();
        return procesados.Select(MapToDto);
    }

    public async Task<IEnumerable<DocumentoProcesadoDto>> ObtenerPorEstadoAsync(string estado)
    {
        if (!Enum.TryParse<EstadoProcesamiento>(estado, out var estadoEnum))
            throw new ArgumentException($"Estado no válido: {estado}");

        var procesados = await _procesamientoRepository.GetByEstadoAsync(estadoEnum);
        return procesados.Select(MapToDto);
    }

    public async Task<DashboardProcesamientoDto> ObtenerDashboardAsync()
    {
        var todos = (await _procesamientoRepository.GetAllAsync()).ToList();

        return new DashboardProcesamientoDto
        {
            TotalDocumentos = todos.Count,
            Pendientes = todos.Count(p => p.Estado == EstadoProcesamiento.Pendiente),
            EnProceso = todos.Count(p => p.Estado == EstadoProcesamiento.EnProceso),
            Procesados = todos.Count(p => p.Estado == EstadoProcesamiento.Procesado),
            ConError = todos.Count(p => p.Estado == EstadoProcesamiento.Error),
            TotalChunks = todos.Where(p => p.Estado == EstadoProcesamiento.Procesado).Sum(p => p.TotalChunks),
            TotalCaracteres = todos.Where(p => p.Estado == EstadoProcesamiento.Procesado).Sum(p => p.TotalCaracteres)
        };
    }

    public async Task<DocumentoProcesadoDto?> ObtenerPorVersionIdAsync(int versionId)
    {
        var procesado = await _procesamientoRepository.GetByVersionIdAsync(versionId);
        if (procesado == null) return null;
        return MapToDto(procesado);
    }

    public async Task<IEnumerable<DocumentoChunkDto>> ObtenerChunksAsync(int procesadoId)
    {
        var procesado = await _procesamientoRepository.GetByIdWithChunksAsync(procesadoId);
        if (procesado == null || procesado.Chunks == null)
            return Enumerable.Empty<DocumentoChunkDto>();

        return procesado.Chunks
            .OrderBy(c => c.Orden)
            .Select(MapChunkToDto);
    }

    public async Task ProcesarDocumentoAsync(int versionId)
    {
        var version = await _versionRepository.GetByIdAsync(versionId);
        if (version == null)
            throw new KeyNotFoundException($"Versión {versionId} no encontrada.");

        var procesado = await _procesamientoRepository.GetByVersionIdAsync(versionId);

        if (procesado != null && procesado.Estado == EstadoProcesamiento.Procesado)
        {
            _logger.LogInformation("La versión {VersionId} ya fue procesada correctamente.", versionId);
            return;
        }

        if (procesado != null)
        {
            procesado.Estado = EstadoProcesamiento.EnProceso;
            procesado.FechaInicio = DateTime.UtcNow;
            procesado.FechaFin = null;
            procesado.Observaciones = "Iniciando procesamiento...";
            procesado.TotalPaginas = 0;
            procesado.TotalCaracteres = 0;
            procesado.TotalChunks = 0;
            _procesamientoRepository.Update(procesado);
        }
        else
        {
            procesado = new DocumentoProcesado
            {
                IdVersionDocumento = versionId,
                FechaInicio = DateTime.UtcNow,
                Estado = EstadoProcesamiento.EnProceso,
                Observaciones = "Iniciando procesamiento..."
            };
            await _procesamientoRepository.AddAsync(procesado);
        }

        await _unitOfWork.SaveChangesAsync();

        try
        {
            // 1. Extraer texto del archivo (PDF o TXT)
            _logger.LogInformation("Extrayendo texto del archivo: {Archivo}", version.RutaArchivo);
            var extraccion = await _extractora.ExtraerTextoAsync(version.RutaArchivo);

            if (!extraccion.Exitoso)
            {
                procesado.Estado = EstadoProcesamiento.Error;
                procesado.FechaFin = DateTime.UtcNow;
                procesado.Observaciones = extraccion.EsDocumentoProtegido
                    ? "Documento protegido con contraseña"
                    : $"Error de extracción: {extraccion.Error}";

                _procesamientoRepository.Update(procesado);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning("Error al extraer texto de versión {VersionId}: {Error}",
                    versionId, procesado.Observaciones);
                return;
            }

            procesado.TotalPaginas = extraccion.TotalPaginas;
            _logger.LogInformation("Texto extraído: {Paginas} páginas, {Caracteres} caracteres",
                extraccion.TotalPaginas, extraccion.TextoCompleto.Length);

            // 2. Normalizar el texto
            _logger.LogInformation("Normalizando texto...");
            var textoNormalizado = _normalizador.Normalizar(extraccion.TextoCompleto);
            procesado.TotalCaracteres = textoNormalizado.Length;

            // 3. Dividir en chunks
            _logger.LogInformation("Dividiendo texto en chunks...");
            var chunkingConfig = new ChunkingConfig
            {
                TamanoMaximoChunk = _config.TamanoMaximoChunk,
                Solapamiento = _config.Solapamiento,
                LongitudMinima = _config.LongitudMinima
            };

            var chunks = _chunkingService.DividirEnChunks(
                textoNormalizado,
                procesado.IdDocumentoProcesado,
                chunkingConfig);

            procesado.TotalChunks = chunks.Count;
            procesado.FechaFin = DateTime.UtcNow;

            await _procesamientoRepository.DeleteChunksByProcesadoIdAsync(procesado.IdDocumentoProcesado);
            await _unitOfWork.SaveChangesAsync();

            if (!chunks.Any())
            {
                _logger.LogWarning("Versión {VersionId}: chunking devolvió 0 chunks. Texto normalizado tenía {Len} caracteres.", versionId, textoNormalizado?.Length ?? 0);
                procesado.Estado = EstadoProcesamiento.Error;
                procesado.Observaciones = $"Procesamiento fallido: 0 chunks generados. Texto: {textoNormalizado?.Length ?? 0} caracteres.";
            }
            else
            {
                procesado.Estado = EstadoProcesamiento.Procesado;
                procesado.Observaciones = $"Procesado exitosamente. {chunks.Count} chunks generados.";
            }

            _procesamientoRepository.Update(procesado);

            if (chunks.Any())
            {
                await _procesamientoRepository.AddChunksAsync(chunks);
            }

            await _unitOfWork.SaveChangesAsync();

            // Actualizar pendienteProcesamiento del documento
            var documento = version.Documento;
            if (documento != null)
            {
                documento.PendienteProcesamiento = false;
                _documentoRepository.Update(documento);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Procesamiento completado para versión {VersionId}: {Chunks} chunks, {Caracteres} caracteres",
                versionId, chunks.Count, textoNormalizado.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al procesar versión {VersionId}", versionId);

            procesado.Estado = EstadoProcesamiento.Error;
            procesado.FechaFin = DateTime.UtcNow;
            procesado.Observaciones = $"Error inesperado: {ex.Message}";

            _procesamientoRepository.Update(procesado);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task ReprocesarDocumentoAsync(int procesadoId)
    {
        var procesado = await _procesamientoRepository.GetByIdAsync(procesadoId);
        if (procesado == null)
            throw new KeyNotFoundException($"Registro de procesamiento {procesadoId} no encontrado.");

        // Resetear estado para forzar re-procesamiento completo
        procesado.Estado = EstadoProcesamiento.Pendiente;
        procesado.Observaciones = "Re-procesamiento iniciado...";
        _procesamientoRepository.Update(procesado);
        await _unitOfWork.SaveChangesAsync();

        // Eliminar chunks existentes (cascada FK)
        // Llamar al procesamiento forzado
        await ProcesarDocumentoForzadoAsync(procesado.IdVersionDocumento);
    }

    /// <summary>
    /// Procesa sin verificar estado previo (para re-procesar).
    /// </summary>
    private async Task ProcesarDocumentoForzadoAsync(int versionId)
    {
        var version = await _versionRepository.GetByIdAsync(versionId);
        if (version == null)
            throw new KeyNotFoundException($"Versión {versionId} no encontrada.");

        var procesado = await _procesamientoRepository.GetByVersionIdAsync(versionId);
        if (procesado == null)
            throw new InvalidOperationException("No hay registro de procesamiento para esta versión.");

        // Ya está en estado Pendiente por el ReprocesarDocumentoAsync
        procesado.Estado = EstadoProcesamiento.EnProceso;
        procesado.FechaInicio = DateTime.UtcNow;
        procesado.FechaFin = null;
        procesado.Observaciones = "Re-procesando...";
        procesado.TotalPaginas = 0;
        procesado.TotalCaracteres = 0;
        procesado.TotalChunks = 0;
        _procesamientoRepository.Update(procesado);

        await _unitOfWork.SaveChangesAsync();

        try
        {
            // 1. Extraer texto del PDF (YA USA LimpiarTextoPdf en ExtractoraTextoService)
            _logger.LogInformation("Extrayendo texto del archivo: {Archivo}", version.RutaArchivo);
            var extraccion = await _extractora.ExtraerTextoPdfAsync(version.RutaArchivo);

            if (!extraccion.Exitoso)
            {
                procesado.Estado = EstadoProcesamiento.Error;
                procesado.FechaFin = DateTime.UtcNow;
                procesado.Observaciones = extraccion.EsDocumentoProtegido
                    ? "Documento protegido con contraseña"
                    : $"Error de extracción: {extraccion.Error}";

                _procesamientoRepository.Update(procesado);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning("Error al extraer texto de versión {VersionId}: {Error}",
                    versionId, procesado.Observaciones);
                return;
            }

            procesado.TotalPaginas = extraccion.TotalPaginas;
            _logger.LogInformation("Texto extraído: {Paginas} páginas, {Caracteres} caracteres",
                extraccion.TotalPaginas, extraccion.TextoCompleto.Length);

            // 2. Normalizar el texto
            _logger.LogInformation("Normalizando texto...");
            var textoNormalizado = _normalizador.Normalizar(extraccion.TextoCompleto);
            procesado.TotalCaracteres = textoNormalizado.Length;

            // 3. Dividir en chunks
            _logger.LogInformation("Dividiendo texto en chunks...");
            var chunkingConfig = new ChunkingConfig
            {
                TamanoMaximoChunk = _config.TamanoMaximoChunk,
                Solapamiento = _config.Solapamiento,
                LongitudMinima = _config.LongitudMinima
            };

            var chunks = _chunkingService.DividirEnChunks(
                textoNormalizado,
                procesado.IdDocumentoProcesado,
                chunkingConfig);

            procesado.TotalChunks = chunks.Count;
            procesado.FechaFin = DateTime.UtcNow;

            // Borrar chunks viejos antes de agregar los nuevos
            await _procesamientoRepository.DeleteChunksByProcesadoIdAsync(procesado.IdDocumentoProcesado);
            await _unitOfWork.SaveChangesAsync();

            if (!chunks.Any())
            {
                _logger.LogWarning("Re-procesamiento versión {VersionId}: chunking devolvió 0 chunks. Texto tenía {Len} caracteres.", versionId, textoNormalizado?.Length ?? 0);
                procesado.Estado = EstadoProcesamiento.Error;
                procesado.Observaciones = $"Re-procesamiento fallido: 0 chunks generados. Texto: {textoNormalizado?.Length ?? 0} caracteres.";
            }
            else
            {
                procesado.Estado = EstadoProcesamiento.Procesado;
                procesado.Observaciones = $"Re-procesado exitosamente. {chunks.Count} chunks generados.";
            }

            _procesamientoRepository.Update(procesado);

            // Guardar chunks nuevos
            if (chunks.Any())
            {
                await _procesamientoRepository.AddChunksAsync(chunks);
            }

            await _unitOfWork.SaveChangesAsync();

            var documento = version.Documento;
            if (documento != null)
            {
                documento.PendienteProcesamiento = false;
                _documentoRepository.Update(documento);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Re-procesamiento completado para versión {VersionId}: {Chunks} chunks, {Caracteres} caracteres",
                versionId, chunks.Count, textoNormalizado.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al re-procesar versión {VersionId}", versionId);

            procesado.Estado = EstadoProcesamiento.Error;
            procesado.FechaFin = DateTime.UtcNow;
            procesado.Observaciones = $"Error inesperado: {ex.Message}";

            _procesamientoRepository.Update(procesado);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<DocumentoProcesadoDto>> ObtenerDocumentosPendientesAsync()
    {
        var pendientes = await _procesamientoRepository.GetByEstadoAsync(EstadoProcesamiento.Pendiente);
        return pendientes.Select(MapToDto);
    }

    public async Task<int> RepararDocumentosConChunksFaltantesAsync()
    {
        var todos = (await _procesamientoRepository.GetAllAsync()).ToList();
        var rotos = todos.Where(p => p.Estado == EstadoProcesamiento.Procesado && p.TotalChunks > 0 && (p.Chunks == null || !p.Chunks.Any())).ToList();

        if (!rotos.Any()) return 0;

        _logger.LogWarning("Detectados {Count} documentos con TotalChunks > 0 pero 0 chunks reales en DB. Reprocesando...", rotos.Count);

        int reparados = 0;
        foreach (var doc in rotos)
        {
            try
            {
                _logger.LogInformation("Reparando documento procesado {Id} (versión {VersionId})...", doc.IdDocumentoProcesado, doc.IdVersionDocumento);
                await ReprocesarDocumentoAsync(doc.IdDocumentoProcesado);
                reparados++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reparar documento procesado {Id}", doc.IdDocumentoProcesado);
            }
        }

        return reparados;
    }

    private static DocumentoProcesadoDto MapToDto(DocumentoProcesado p)
    {
        return new DocumentoProcesadoDto
        {
            IdDocumentoProcesado = p.IdDocumentoProcesado,
            IdVersionDocumento = p.IdVersionDocumento,
            IdDocumento = p.VersionDocumento?.Documento?.IdDocumento ?? 0,
            DocumentoNombre = p.VersionDocumento?.Documento?.Nombre ?? "—",
            DocumentoCodigo = p.VersionDocumento?.Documento?.Codigo ?? "—",
            NumeroVersion = p.VersionDocumento?.NumeroVersion ?? 0,
            NombreArchivo = p.VersionDocumento?.NombreArchivo ?? "—",
            FechaInicio = p.FechaInicio,
            FechaFin = p.FechaFin,
            Estado = p.Estado.ToString(),
            TotalPaginas = p.TotalPaginas,
            TotalCaracteres = p.TotalCaracteres,
            TotalChunks = p.TotalChunks,
            Observaciones = p.Observaciones
        };
    }

    private static DocumentoChunkDto MapChunkToDto(DocumentoChunk c)
    {
        return new DocumentoChunkDto
        {
            IdChunk = c.IdChunk,
            IdDocumentoProcesado = c.IdDocumentoProcesado,
            NumeroChunk = c.NumeroChunk,
            PaginaInicial = c.PaginaInicial,
            PaginaFinal = c.PaginaFinal,
            Texto = c.Texto,
            TotalCaracteres = c.TotalCaracteres,
            Orden = c.Orden
        };
    }
}
