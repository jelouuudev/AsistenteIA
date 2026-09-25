using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class IndexacionService : IIndexacionService
{
    private readonly IDocumentoIndexadoRepository _indexadoRepository;
    private readonly IProcesamientoDocumentalRepository _procesamientoRepository;
    private readonly IDocumentoRepository _documentoRepository;
    private readonly IDocumentoVersionRepository _versionRepository;
    private readonly IVectorStore _vectorStore;
    private readonly EmbeddingService _embeddingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IndexacionService> _logger;
    private readonly IDocumentoFuenteRepository _documentoFuenteRepository;

    public IndexacionService(
        IDocumentoIndexadoRepository indexadoRepository,
        IProcesamientoDocumentalRepository procesamientoRepository,
        IDocumentoRepository documentoRepository,
        IDocumentoVersionRepository versionRepository,
        IVectorStore vectorStore,
        EmbeddingService embeddingService,
        IUnitOfWork unitOfWork,
        ILogger<IndexacionService> logger,
        IDocumentoFuenteRepository documentoFuenteRepository)
    {
        _indexadoRepository = indexadoRepository;
        _procesamientoRepository = procesamientoRepository;
        _documentoRepository = documentoRepository;
        _versionRepository = versionRepository;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _documentoFuenteRepository = documentoFuenteRepository;
    }

    public async Task<DocumentoIndexadoDto?> ObtenerPorIdAsync(int id)
    {
        var indexado = await _indexadoRepository.GetByIdAsync(id);
        if (indexado == null) return null;
        return MapToDto(indexado);
    }

    public async Task<DocumentoIndexadoDto?> ObtenerPorProcesadoIdAsync(int procesadoId)
    {
        var indexado = await _indexadoRepository.GetByProcesadoIdAsync(procesadoId);
        if (indexado == null) return null;
        return MapToDto(indexado);
    }

    public async Task<IEnumerable<DocumentoIndexadoDto>> ObtenerTodosAsync()
    {
        var indexados = await _indexadoRepository.GetAllAsync();
        return indexados.Select(MapToDto);
    }

    public async Task<IEnumerable<DocumentoIndexadoDto>> ObtenerPorEstadoAsync(string estado)
    {
        if (!Enum.TryParse<EstadoIndexacion>(estado, out var estadoEnum))
            throw new ArgumentException($"Estado no válido: {estado}");

        var indexados = await _indexadoRepository.GetByEstadoAsync(estadoEnum);
        return indexados.Select(MapToDto);
    }

    public async Task<DashboardIndexacionDto> ObtenerDashboardAsync()
    {
        var todos = (await _indexadoRepository.GetAllAsync()).ToList();

        var totalChunks = todos.Where(i => i.Estado == EstadoIndexacion.Indexado).Sum(i => i.TotalChunks);
        var totalEmbeddings = todos.Where(i => i.Estado == EstadoIndexacion.Indexado).Sum(i => i.TotalEmbeddings);

        bool estadoBaseVectorial = false;
        try
        {
            estadoBaseVectorial = await _vectorStore.HealthCheckAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo verificar el estado de la base vectorial.");
        }

        return new DashboardIndexacionDto
        {
            TotalDocumentosIndexados = todos.Count(i => i.Estado == EstadoIndexacion.Indexado),
            TotalChunksIndexados = totalChunks,
            TotalEmbeddings = totalEmbeddings,
            PendientesIndexacion = todos.Count(i => i.Estado == EstadoIndexacion.Pendiente),
            EnProcesoIndexacion = todos.Count(i => i.Estado == EstadoIndexacion.EnProceso),
            ConErrorIndexacion = todos.Count(i => i.Estado == EstadoIndexacion.Error),
            TiempoPromedioIndexacionMs = await _indexadoRepository.GetTiempoPromedioIndexacionAsync(),
            EstadoBaseVectorial = estadoBaseVectorial
        };
    }

    public async Task IndexarDocumentoAsync(int documentoProcesadoId)
    {
        _logger.LogInformation("Iniciando indexación del documento procesado {Id}.", documentoProcesadoId);

        var procesado = await _procesamientoRepository.GetByIdWithChunksAsync(documentoProcesadoId);
        if (procesado == null)
            throw new KeyNotFoundException($"Documento procesado {documentoProcesadoId} no encontrado.");

        if (procesado.Estado != EstadoProcesamiento.Procesado)
            throw new InvalidOperationException($"El documento procesado {documentoProcesadoId} no tiene estado Procesado.");

        // Solo documentos Activos se indexan. Archivados, borradores y eliminados
        // no deben tener vectores (archivar revoca el acceso).
        if (!await EsDocumentoActivoAsync(procesado.IdVersionDocumento))
        {
            _logger.LogInformation("Documento procesado {Id} omitido en indexación: su documento no está Activo.", documentoProcesadoId);
            return;
        }

        // Solo la versión vigente es buscable. Si la versión está inactiva, se
        // garantiza que no queden vectores y se marca Excluido (no Error) para que
        // el servicio de fondo no lo reintente en cada ciclo (evita resucitar
        // vectores de versiones viejas).
        var version = await _versionRepository.GetByIdAsync(procesado.IdVersionDocumento);
        if (version == null || !version.Activo)
        {
            _logger.LogInformation("Documento procesado {Id} omitido en indexación: su versión no está activa.", documentoProcesadoId);
            try
            {
                await _vectorStore.DeleteByDocumentoProcesadoIdAsync(documentoProcesadoId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar vectores residuales del procesado {Id}.", documentoProcesadoId);
            }
            var idxExistente = await _indexadoRepository.GetByProcesadoIdAsync(documentoProcesadoId);
            if (idxExistente == null)
            {
                await _indexadoRepository.AddAsync(new DocumentoIndexado
                {
                    IdDocumentoProcesado = documentoProcesadoId,
                    FechaIndexacion = DateTime.UtcNow,
                    Estado = EstadoIndexacion.Excluido,
                    Observaciones = "Versión no vigente: excluida de la búsqueda."
                });
            }
            else if (idxExistente.Estado != EstadoIndexacion.Excluido)
            {
                idxExistente.Estado = EstadoIndexacion.Excluido;
                idxExistente.FechaIndexacion = DateTime.UtcNow;
                idxExistente.Observaciones = "Versión no vigente: excluida de la búsqueda.";
                _indexadoRepository.Update(idxExistente);
            }
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        var indexadoExistente = await _indexadoRepository.GetByProcesadoIdAsync(documentoProcesadoId);
        if (indexadoExistente != null && indexadoExistente.Estado == EstadoIndexacion.Indexado)
        {
            _logger.LogInformation("El documento procesado {Id} ya está indexado.", documentoProcesadoId);
            return;
        }

        DocumentoIndexado indexado;
        if (indexadoExistente != null)
        {
            indexado = indexadoExistente;
            indexado.Estado = EstadoIndexacion.EnProceso;
            indexado.FechaIndexacion = DateTime.UtcNow;
            indexado.Observaciones = "Iniciando indexación...";
            _indexadoRepository.Update(indexado);
        }
        else
        {
            indexado = new DocumentoIndexado
            {
                IdDocumentoProcesado = documentoProcesadoId,
                FechaIndexacion = DateTime.UtcNow,
                Estado = EstadoIndexacion.EnProceso,
                Observaciones = "Iniciando indexación..."
            };
            await _indexadoRepository.AddAsync(indexado);
        }

        await _unitOfWork.SaveChangesAsync();

        try
        {
            var chunks = procesado.Chunks?.OrderBy(c => c.Orden).ToList();
            if (chunks == null || !chunks.Any())
            {
                _logger.LogWarning("Documento procesado {Id} tiene 0 chunks en la DB (TotalChunks={TotalChunks}, Estado={Estado}). No se puede indexar.",
                    documentoProcesadoId, procesado.TotalChunks, procesado.Estado);
                indexado.Estado = EstadoIndexacion.Error;
                indexado.FechaIndexacion = DateTime.UtcNow;
                indexado.Observaciones = $"No hay chunks para indexar. TotalChunks en DB: {procesado.TotalChunks}, Estado procesamiento: {procesado.Estado}";
                _indexadoRepository.Update(indexado);
                await _unitOfWork.SaveChangesAsync();
                return;
            }

            indexado.TotalChunks = chunks.Count;

            string? docNombre = procesado.VersionDocumento?.Documento?.Nombre;
            string? docCodigo = procesado.VersionDocumento?.Documento?.Codigo;
            int? idDocumento = procesado.VersionDocumento?.IdDocumento;
            int? idVersion = procesado.IdVersionDocumento;
            int? idCategoria = procesado.VersionDocumento?.Documento?.IdCategoria;
            string? versionDocumento = procesado.VersionDocumento?.NumeroVersion.ToString();
            string? estadoDocumento = procesado.VersionDocumento?.Documento?.Estado.ToString();

            var fuentes = idDocumento.HasValue 
                ? await _documentoFuenteRepository.GetByDocumentoIdAsync(idDocumento.Value) 
                : Enumerable.Empty<DocumentoFuente>();
            var idsFuentes = fuentes.Where(f => f.Activo).Select(f => f.IdFuente).ToList();

            int embeddingsGenerados = 0;

            foreach (var chunk in chunks)
            {
                float[]? embedding = null;
                try
                {
                    embedding = await _embeddingService.GenerarEmbeddingAsync(chunk.Texto);
                    if (embedding == null || embedding.Length == 0)
                    {
                        _logger.LogWarning("Embedding vacío para chunk {ChunkId}. No se indexará.", chunk.IdChunk);
                        continue;
                    }
                    embeddingsGenerados++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al generar embedding para chunk {ChunkId}. No se indexará.", chunk.IdChunk);
                    continue;
                }

                if (idsFuentes.Any())
                {
                    foreach (var idFuente in idsFuentes)
                    {
                        var vectorDoc = new VectorDocument
                        {
                            DocumentId = Guid.NewGuid(),
                            ChunkId = chunk.IdChunk,
                            DocumentoProcesadoId = documentoProcesadoId,
                            Text = chunk.Texto,
                            Embedding = embedding ?? Array.Empty<float>(),
                            Orden = chunk.Orden,
                            PaginaInicial = chunk.PaginaInicial,
                            PaginaFinal = chunk.PaginaFinal,
                            MetadataDocumentoNombre = docNombre,
                            MetadataDocumentoCodigo = docCodigo,
                            IdDocumento = idDocumento,
                            IdVersion = idVersion,
                            IdCategoria = idCategoria,
                            VersionDocumento = versionDocumento,
                            EstadoDocumento = estadoDocumento,
                            IdFuente = idFuente
                        };

                        await _vectorStore.IndexAsync(vectorDoc);
                    }
                }
                else
                {
                    var vectorDoc = new VectorDocument
                    {
                        DocumentId = Guid.NewGuid(),
                        ChunkId = chunk.IdChunk,
                        DocumentoProcesadoId = documentoProcesadoId,
                        Text = chunk.Texto,
                        Embedding = embedding ?? Array.Empty<float>(),
                        Orden = chunk.Orden,
                        PaginaInicial = chunk.PaginaInicial,
                        PaginaFinal = chunk.PaginaFinal,
                        MetadataDocumentoNombre = docNombre,
                        MetadataDocumentoCodigo = docCodigo,
                        IdDocumento = idDocumento,
                        IdVersion = idVersion,
                        IdCategoria = idCategoria,
                        VersionDocumento = versionDocumento,
                        EstadoDocumento = estadoDocumento,
                        IdFuente = null
                    };

                    await _vectorStore.IndexAsync(vectorDoc);
                }
            }

            indexado.TotalEmbeddings = embeddingsGenerados;
            indexado.Estado = EstadoIndexacion.Indexado;
            indexado.FechaIndexacion = DateTime.UtcNow;
            indexado.Observaciones = $"Indexación completada. {embeddingsGenerados}/{chunks.Count} embeddings generados. {chunks.Count} chunks almacenados.";

            _indexadoRepository.Update(indexado);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Indexación completada para documento procesado {Id}: {Embeddings}/{Chunks} embeddings.",
                documentoProcesadoId, embeddingsGenerados, chunks.Count);

            // Solo la versión vigente es buscable: eliminar vectores de versiones
            // anteriores (inactivas) del mismo documento.
            await LimpiarVectoresVersionesAnterioresAsync(idDocumento, procesado.IdVersionDocumento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al indexar documento procesado {Id}.", documentoProcesadoId);

            indexado.Estado = EstadoIndexacion.Error;
            indexado.FechaIndexacion = DateTime.UtcNow;
            indexado.Observaciones = $"Error inesperado: {ex.Message}";

            _indexadoRepository.Update(indexado);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task ReindexarDocumentoAsync(int documentoProcesadoId)
    {
        _logger.LogInformation("Reindexando documento procesado {Id}.", documentoProcesadoId);

        var indexado = await _indexadoRepository.GetByProcesadoIdAsync(documentoProcesadoId);
        if (indexado != null)
        {
            try
            {
                await _vectorStore.DeleteByDocumentoProcesadoIdAsync(documentoProcesadoId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar vectores anteriores del documento procesado {Id}.", documentoProcesadoId);
            }

            _indexadoRepository.Delete(indexado);
            await _unitOfWork.SaveChangesAsync();
        }

        await IndexarDocumentoAsync(documentoProcesadoId);
    }

    /// <summary>
    /// Reindexa todos los procesados activos de un documento. Debe llamarse tras
    /// cambiar las fuentes asignadas al documento para que los vectores reflejen
    /// la asignacion vigente (evita vectores huerfanos con fuentes anteriores).
    /// </summary>
    public async Task ReindexarPorDocumentoAsync(int idDocumento)
    {
        _logger.LogInformation("Reindexando vectores por cambio de fuentes del documento {IdDocumento}.", idDocumento);

        // Un documento no activo no debe recuperar vectores (p. ej. reasignar
        // fuentes a un archivado no le devuelve el acceso).
        var documento = await _documentoRepository.GetByIdAsync(idDocumento);
        if (documento == null || documento.Estado != EstadoDocumento.Activo)
        {
            _logger.LogInformation("Reindexación omitida: documento {IdDocumento} no está Activo.", idDocumento);
            return;
        }

        var versiones = await _versionRepository.GetByDocumentoIdAsync(idDocumento) ?? Enumerable.Empty<DocumentoVersion>();
        foreach (var version in versiones.Where(v => v.Activo))
        {
            var procesado = await _procesamientoRepository.GetByVersionIdAsync(version.IdVersion);
            if (procesado == null || procesado.Estado != EstadoProcesamiento.Procesado)
                continue;

            try
            {
                await ReindexarDocumentoAsync(procesado.IdDocumentoProcesado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reindexar documento procesado {Id} tras cambio de fuentes del documento {IdDocumento}.",
                    procesado.IdDocumentoProcesado, idDocumento);
            }
        }
    }

    public async Task ReindexarCategoriaAsync(int categoriaId)
    {
        _logger.LogInformation("Reindexando categoría {CategoriaId}.", categoriaId);

        var documentos = await _documentoRepository.GetAllAsync();
        var documentosDeCategoria = documentos.Where(d => d.IdCategoria == categoriaId && d.Estado == EstadoDocumento.Activo);

        foreach (var documento in documentosDeCategoria)
        {
            var version = documento.Versiones?
                .Where(v => v.NumeroVersion == documento.VersionActual)
                .FirstOrDefault();

            if (version == null) continue;

            var procesado = await _procesamientoRepository.GetByVersionIdAsync(version.IdVersion);
            if (procesado != null && procesado.Estado == EstadoProcesamiento.Procesado)
            {
                try
                {
                    await ReindexarDocumentoAsync(procesado.IdDocumentoProcesado);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al reindexar documento procesado {Id} en categoría {CategoriaId}.",
                        procesado.IdDocumentoProcesado, categoriaId);
                }
            }
        }
    }

    public async Task ReindexarTodosAsync()
    {
        _logger.LogInformation("Reindexando todos los documentos.");

        await _vectorStore.ClearAsync();

        var procesados = await _procesamientoRepository.GetAllAsync();
        var documentosProcesados = procesados.Where(p => p.Estado == EstadoProcesamiento.Procesado);

        foreach (var procesado in documentosProcesados)
        {
            try
            {
                // No resucitar vectores de documentos no activos.
                if (!await EsDocumentoActivoAsync(procesado.IdVersionDocumento))
                {
                    _logger.LogInformation("Reindexación total omite procesado {Id}: documento no Activo.", procesado.IdDocumentoProcesado);
                    continue;
                }

                await ReindexarDocumentoAsync(procesado.IdDocumentoProcesado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reindexar documento procesado {Id}.", procesado.IdDocumentoProcesado);
            }
        }
    }

    private async Task<bool> EsDocumentoActivoAsync(int versionId)
    {
        var version = await _versionRepository.GetByIdAsync(versionId);
        if (version == null) return false;
        var documento = await _documentoRepository.GetByIdAsync(version.IdDocumento);
        return documento != null && documento.Estado == EstadoDocumento.Activo;
    }

    private async Task LimpiarVectoresVersionesAnterioresAsync(int? idDocumento, int versionActualId)
    {
        if (!idDocumento.HasValue) return;
        var versiones = await _versionRepository.GetByDocumentoIdAsync(idDocumento.Value) ?? Enumerable.Empty<DocumentoVersion>();
        foreach (var v in versiones.Where(v => v.IdVersion != versionActualId && !v.Activo))
        {
            var proc = await _procesamientoRepository.GetByVersionIdAsync(v.IdVersion);
            if (proc == null) continue;
            _logger.LogInformation("Eliminando vectores de versión anterior (procesado {Id}).", proc.IdDocumentoProcesado);
            await EliminarIndiceAsync(proc.IdDocumentoProcesado);
        }
    }

    public async Task EliminarIndiceAsync(int documentoProcesadoId)
    {
        _logger.LogInformation("Eliminando índice del documento procesado {Id}.", documentoProcesadoId);

        try
        {
            await _vectorStore.DeleteByDocumentoProcesadoIdAsync(documentoProcesadoId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar vectores del documento procesado {Id}.", documentoProcesadoId);
        }

        var indexado = await _indexadoRepository.GetByProcesadoIdAsync(documentoProcesadoId);
        if (indexado != null)
        {
            _indexadoRepository.Delete(indexado);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task LimpiarVectorStoreAsync()
    {
        _logger.LogInformation("Limpiando completamente el vectorstore.");
        await _vectorStore.ClearAsync();
        _logger.LogInformation("Vectorstore limpiado exitosamente.");
    }

    private static DocumentoIndexadoDto MapToDto(DocumentoIndexado i)
    {
        return new DocumentoIndexadoDto
        {
            IdDocumentoIndexado = i.IdDocumentoIndexado,
            IdDocumentoProcesado = i.IdDocumentoProcesado,
            IdDocumento = i.DocumentoProcesado?.VersionDocumento?.Documento?.IdDocumento ?? 0,
            DocumentoNombre = i.DocumentoProcesado?.VersionDocumento?.Documento?.Nombre ?? "—",
            DocumentoCodigo = i.DocumentoProcesado?.VersionDocumento?.Documento?.Codigo ?? "—",
            FechaIndexacion = i.FechaIndexacion,
            Estado = i.Estado.ToString(),
            TotalChunks = i.TotalChunks,
            TotalEmbeddings = i.TotalEmbeddings,
            Observaciones = i.Observaciones
        };
    }
}
