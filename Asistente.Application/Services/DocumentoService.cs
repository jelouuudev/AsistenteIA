using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class DocumentoService : IDocumentoService
{
    private readonly IDocumentoRepository _documentoRepository;
    private readonly IDocumentoVersionRepository _versionRepository;
    private readonly ICategoriaDocumentoRepository _categoriaRepository;
    private readonly IAuditoriaDocumentalRepository _auditoriaDocRepository;
    private readonly IProcesamientoDocumentalRepository _procesamientoRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentoFuenteRepository _documentoFuenteRepository;
    private readonly ILogger<DocumentoService> _logger;

    public DocumentoService(
        IDocumentoRepository documentoRepository,
        IDocumentoVersionRepository versionRepository,
        ICategoriaDocumentoRepository categoriaRepository,
        IAuditoriaDocumentalRepository auditoriaDocRepository,
        IProcesamientoDocumentalRepository procesamientoRepository,
        IFileStorageService fileStorageService,
        IAuditoriaService auditoriaService,
        IUnitOfWork unitOfWork,
        IDocumentoFuenteRepository documentoFuenteRepository,
        ILogger<DocumentoService> logger)
    {
        _documentoRepository = documentoRepository;
        _versionRepository = versionRepository;
        _categoriaRepository = categoriaRepository;
        _auditoriaDocRepository = auditoriaDocRepository;
        _procesamientoRepository = procesamientoRepository;
        _fileStorageService = fileStorageService;
        _auditoriaService = auditoriaService;
        _unitOfWork = unitOfWork;
        _documentoFuenteRepository = documentoFuenteRepository;
        _logger = logger;
    }

    public async Task<DocumentoDto?> ObtenerPorIdAsync(int id)
    {
        var documento = await _documentoRepository.GetByIdAsync(id);
        if (documento == null) return null;
        return MapToDto(documento);
    }

    public async Task<IEnumerable<DocumentoDto>> ObtenerTodosAsync()
    {
        var documentos = await _documentoRepository.GetAllAsync();
        return documentos.Select(MapToDto);
    }

    public async Task<IEnumerable<DocumentoDto>> ObtenerFiltradosAsync(FiltroDocumentoRequest filtro)
    {
        EstadoDocumento? estadoEnum = null;
        if (!string.IsNullOrEmpty(filtro.Estado) && Enum.TryParse<EstadoDocumento>(filtro.Estado, out var parsed))
        {
            estadoEnum = parsed;
        }

        var documentos = await _documentoRepository.GetFilteredAsync(
            filtro.Nombre, filtro.IdCategoria, estadoEnum, filtro.FechaDesde, filtro.FechaHasta);

        return documentos.Select(MapToDto);
    }

    public async Task<DocumentoDto> CrearAsync(CrearDocumentoRequest request, int currentUserId, string ipAddress)
    {
        // Validar categoría
        var categoria = await _categoriaRepository.GetByIdAsync(request.IdCategoria);
        if (categoria == null)
        {
            throw new KeyNotFoundException("La categoría seleccionada no existe.");
        }

        // Validar código único (solo si no está eliminado)
        var existente = await _documentoRepository.GetByCodigoAsync(request.Codigo);
        if (existente != null && existente.Estado != EstadoDocumento.Eliminado)
        {
            throw new InvalidOperationException($"Ya existe un documento activo con el código '{request.Codigo}'. Elimínalo primero o usa otro código.");
        }

        var documento = new Documento
        {
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            IdCategoria = request.IdCategoria,
            VersionActual = 0,
            Estado = EstadoDocumento.Activo,
            PendienteProcesamiento = true,
            FechaRegistro = DateTime.UtcNow,
            UsuarioRegistro = currentUserId
        };

        await _documentoRepository.AddAsync(documento);
        await _unitOfWork.SaveChangesAsync();

        // ETAPA 16 (Opción A): vincular automáticamente el documento a su Fuente de
        // Conocimiento RAG según la categoría. Así el agente que tenga esa fuente
        // asignada leerá el documento sin vinculación manual (Regla 2).
        await VincularDocumentoAFuentePorCategoriaAsync(documento.IdDocumento, request.IdCategoria);

        // Auditoría documental
        await RegistrarAuditoriaDocumentalAsync(
            documento.IdDocumento, null, "Creación",
            $"Se creó el documento '{documento.Nombre}' con código '{documento.Codigo}'",
            currentUserId, ipAddress);

        _logger.LogInformation("Documento '{Nombre}' (código: {Codigo}) creado por usuario {UserId}",
            documento.Nombre, documento.Codigo, currentUserId);

        // Reload with includes
        var result = await _documentoRepository.GetByIdAsync(documento.IdDocumento);
        return MapToDto(result!);
    }

    public async Task<DocumentoDto> ActualizarAsync(int id, ActualizarDocumentoRequest request, int currentUserId, string ipAddress)
    {
        var documento = await _documentoRepository.GetByIdAsync(id);
        if (documento == null)
        {
            throw new KeyNotFoundException("Documento no encontrado.");
        }

        if (documento.Estado == EstadoDocumento.Eliminado)
        {
            throw new InvalidOperationException("No se puede modificar un documento eliminado.");
        }

        var oldNombre = documento.Nombre;
        var oldCategoria = documento.IdCategoria;

        documento.Nombre = request.Nombre;
        documento.Descripcion = request.Descripcion;
        documento.IdCategoria = request.IdCategoria;
        documento.PendienteProcesamiento = true;

        _documentoRepository.Update(documento);
        await _unitOfWork.SaveChangesAsync();

        var cambios = new List<string>();
        if (oldNombre != request.Nombre) cambios.Add($"Nombre a '{request.Nombre}'");
        if (oldCategoria != request.IdCategoria) cambios.Add($"Categoría cambiada");

        await RegistrarAuditoriaDocumentalAsync(
            documento.IdDocumento, null, "Modificación",
            $"Se modificó el documento '{oldNombre}': {(cambios.Any() ? string.Join(", ", cambios) : "Sin cambios significativos")}",
            currentUserId, ipAddress);

        var result = await _documentoRepository.GetByIdAsync(documento.IdDocumento);
        return MapToDto(result!);
    }

    public async Task ActivarAsync(int id, int currentUserId, string ipAddress)
    {
        var documento = await _documentoRepository.GetByIdAsync(id);
        if (documento == null)
            throw new KeyNotFoundException("Documento no encontrado.");

        if (documento.Estado == EstadoDocumento.Eliminado)
            throw new InvalidOperationException("No se puede activar un documento eliminado.");

        var oldEstado = documento.Estado;
        documento.Estado = EstadoDocumento.Activo;

        _documentoRepository.Update(documento);
        await _unitOfWork.SaveChangesAsync();

        await RegistrarAuditoriaDocumentalAsync(
            documento.IdDocumento, null, "CambioEstado",
            $"Estado cambiado de '{oldEstado}' a 'Activo'",
            currentUserId, ipAddress);
    }

    public async Task ArchivarAsync(int id, int currentUserId, string ipAddress)
    {
        var documento = await _documentoRepository.GetByIdAsync(id);
        if (documento == null)
            throw new KeyNotFoundException("Documento no encontrado.");

        if (documento.Estado == EstadoDocumento.Eliminado)
            throw new InvalidOperationException("No se puede archivar un documento eliminado.");

        var oldEstado = documento.Estado;
        documento.Estado = EstadoDocumento.Archivado;

        _documentoRepository.Update(documento);
        await _unitOfWork.SaveChangesAsync();

        await RegistrarAuditoriaDocumentalAsync(
            documento.IdDocumento, null, "CambioEstado",
            $"Estado cambiado de '{oldEstado}' a 'Archivado'",
            currentUserId, ipAddress);
    }

    public async Task EliminarAsync(int id, int currentUserId, string ipAddress)
    {
        var documento = await _documentoRepository.GetByIdAsync(id);
        if (documento == null)
            throw new KeyNotFoundException("Documento no encontrado.");

        var oldEstado = documento.Estado;
        documento.Estado = EstadoDocumento.Eliminado;

        _documentoRepository.Update(documento);
        await _unitOfWork.SaveChangesAsync();

        await RegistrarAuditoriaDocumentalAsync(
            documento.IdDocumento, null, "EliminaciónLógica",
            $"Documento eliminado lógicamente (estado anterior: '{oldEstado}')",
            currentUserId, ipAddress);

        _logger.LogInformation("Documento {Id} eliminado lógicamente por usuario {UserId}", id, currentUserId);
    }

    // --- Versiones ---

    public async Task<IEnumerable<DocumentoVersionDto>> ObtenerVersionesAsync(int documentoId)
    {
        var versiones = await _versionRepository.GetByDocumentoIdAsync(documentoId);
        return versiones.Select(MapVersionToDto);
    }

    public async Task<DocumentoVersionDto> CargarVersionAsync(int documentoId, string nombreArchivo, Stream archivoStream, int currentUserId, string ipAddress)
    {
        var documento = await _documentoRepository.GetByIdAsync(documentoId);
        if (documento == null)
            throw new KeyNotFoundException("Documento no encontrado.");

        if (documento.Estado == EstadoDocumento.Eliminado)
            throw new InvalidOperationException("No se puede cargar una versión a un documento eliminado.");

        // Validar archivo
        _fileStorageService.ValidateFile(nombreArchivo, archivoStream);

        // Calcular hash
        archivoStream.Position = 0;
        var hash = await CalcularHashAsync(archivoStream);
        archivoStream.Position = 0;

        // Guardar archivo en el repositorio
        var nuevoNumeroVersion = documento.VersionActual + 1;
        var subDirectory = $"doc_{documento.IdDocumento}";
        var archivoNombre = $"v{nuevoNumeroVersion}_{nombreArchivo}";

        var rutaArchivo = await _fileStorageService.SaveFileAsync(archivoStream, archivoNombre, subDirectory);

        // Crear versión
        var version = new DocumentoVersion
        {
            IdDocumento = documentoId,
            NumeroVersion = nuevoNumeroVersion,
            NombreArchivo = nombreArchivo,
            RutaArchivo = rutaArchivo,
            TamanoArchivo = archivoStream.Length,
            HashArchivo = hash,
            FechaCarga = DateTime.UtcNow,
            UsuarioCarga = currentUserId,
            Activo = true
        };

        await _versionRepository.AddAsync(version);

        // Actualizar documento
        documento.VersionActual = nuevoNumeroVersion;
        documento.PendienteProcesamiento = true;
        _documentoRepository.Update(documento);

        await _unitOfWork.SaveChangesAsync();

        var procesado = new DocumentoProcesado
        {
            IdVersionDocumento = version.IdVersion,
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoProcesamiento.Pendiente,
            Observaciones = "Pendiente de procesamiento"
        };

        await _procesamientoRepository.AddAsync(procesado);
        await _unitOfWork.SaveChangesAsync();

        // Auditoría
        await RegistrarAuditoriaDocumentalAsync(
            documentoId, version.IdVersion, "Carga",
            $"Se cargó la versión {nuevoNumeroVersion} del archivo '{nombreArchivo}' ({version.TamanoArchivo} bytes)",
            currentUserId, ipAddress);

        _logger.LogInformation("Versión {Version} cargada para documento {DocId} por usuario {UserId}. Registro de procesamiento creado.",
            nuevoNumeroVersion, documentoId, currentUserId);

        return MapVersionToDto(version);
    }

    public async Task<(Stream fileStream, string fileName, string contentType)> DescargarVersionAsync(int documentoId, int versionId, int currentUserId, string ipAddress)
    {
        var version = await _versionRepository.GetByIdAsync(versionId);
        if (version == null || version.IdDocumento != documentoId)
            throw new KeyNotFoundException("Versión no encontrada.");

        var fileStream = await _fileStorageService.GetFileAsync(version.RutaArchivo);

        // Registrar auditoría de descarga
        await RegistrarAuditoriaDocumentalAsync(
            documentoId, versionId, "Descarga",
            $"Se descargó la versión {version.NumeroVersion} del archivo '{version.NombreArchivo}'",
            currentUserId, ipAddress);

        _logger.LogInformation("Versión {VersionId} descargada del documento {DocId} por usuario {UserId}",
            versionId, documentoId, currentUserId);

        return (fileStream, version.NombreArchivo, "application/pdf");
    }

    // --- Auditoría ---

    public async Task<IEnumerable<AuditoriaDocumentalDto>> ObtenerAuditoriaAsync(int documentoId)
    {
        var auditorias = await _auditoriaDocRepository.GetByDocumentoIdAsync(documentoId);
        return auditorias.Select(MapAuditoriaToDto);
    }

    public async Task<IEnumerable<AuditoriaDocumentalDto>> ObtenerTodasAuditoriasAsync()
    {
        var auditorias = await _auditoriaDocRepository.GetAllAsync();
        return auditorias.Select(MapAuditoriaToDto);
    }

    // --- Helpers privados ---

    private async Task RegistrarAuditoriaDocumentalAsync(int documentoId, int? versionId, string accion, string descripcion, int userId, string? ip)
    {
        var auditoria = new AuditoriaDocumental
        {
            IdDocumento = documentoId,
            IdVersion = versionId,
            Accion = accion,
            Descripcion = descripcion,
            UsuarioId = userId,
            FechaAccion = DateTime.UtcNow,
            DireccionIP = ip
        };

        await _auditoriaDocRepository.AddAsync(auditoria);
        await _unitOfWork.SaveChangesAsync();

        // También registrar en la auditoría general del sistema
        await _auditoriaService.RegistrarActividadAsync(
            userId, "GestorDocumental", accion, descripcion, ip);
    }

    private static async Task<string> CalcularHashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static DocumentoDto MapToDto(Documento doc)
    {
        return new DocumentoDto
        {
            IdDocumento = doc.IdDocumento,
            Codigo = doc.Codigo,
            Nombre = doc.Nombre,
            Descripcion = doc.Descripcion,
            IdCategoria = doc.IdCategoria,
            CategoriaNombre = doc.Categoria?.Nombre ?? "Sin categoría",
            VersionActual = doc.VersionActual,
            Estado = doc.Estado.ToString(),
            PendienteProcesamiento = doc.PendienteProcesamiento,
            FechaRegistro = doc.FechaRegistro,
            UsuarioRegistro = doc.UsuarioRegistro
        };
    }

    private static DocumentoVersionDto MapVersionToDto(DocumentoVersion v)
    {
        return new DocumentoVersionDto
        {
            IdVersion = v.IdVersion,
            IdDocumento = v.IdDocumento,
            NumeroVersion = v.NumeroVersion,
            NombreArchivo = v.NombreArchivo,
            TamanoArchivo = v.TamanoArchivo,
            HashArchivo = v.HashArchivo,
            FechaCarga = v.FechaCarga,
            UsuarioCarga = v.UsuarioCarga,
            Activo = v.Activo
        };
    }

    private static AuditoriaDocumentalDto MapAuditoriaToDto(AuditoriaDocumental a)
    {
        return new AuditoriaDocumentalDto
        {
            IdAuditoria = a.IdAuditoria,
            IdDocumento = a.IdDocumento,
            DocumentoNombre = a.Documento?.Nombre ?? "—",
            IdVersion = a.IdVersion,
            Accion = a.Accion,
            Descripcion = a.Descripcion,
            UsuarioId = a.UsuarioId,
            FechaAccion = a.FechaAccion,
            DireccionIP = a.DireccionIP
        };
    }

    /// <summary>
    /// ETAPA 16 (Opción A): mapea la categoría del documento a su Fuente de Conocimiento
    /// RAG y crea la vinculación en DocumentoFuente. De este modo, el agente que tenga
    /// asignada esa fuente leerá el documento automáticamente (Regla 2), sin necesidad
    /// de vinculaciones manuales.
    /// </summary>
    private async Task VincularDocumentoAFuentePorCategoriaAsync(int idDocumento, int idCategoria)
    {
        // Mapeo categoria de documento -> fuente de conocimiento RAG (según catálogos en BD).
        var mapeoCategoriaAFuente = new Dictionary<int, int>
        {
            { 1, 1 }, // Manual Usuario        -> Manuales de Usuario
            { 2, 2 }, // Manual Técnico        -> Documentación Técnica
            { 3, 3 }, // Procedimientos        -> Procedimientos Operativos
            { 4, 4 }, // Políticas             -> Políticas y Normativas
            { 5, 4 }, // Normativas            -> Políticas y Normativas
            { 6, 5 }, // FAQ                   -> Base de Datos Empresarial
            { 7, 1 }  // Capacitaciones        -> Manuales de Usuario
        };

        if (!mapeoCategoriaAFuente.TryGetValue(idCategoria, out var idFuente))
        {
            _logger.LogWarning("No se encontró fuente RAG para la categoría {IdCategoria}. El documento {IdDocumento} no quedará vinculado a ninguna fuente.", idCategoria, idDocumento);
            return;
        }

        // Evitar duplicados si ya existe la vinculación.
        var existente = await _documentoFuenteRepository.GetByClaveAsync(idDocumento, idFuente);
        if (existente != null)
        {
            if (!existente.Activo)
            {
                existente.Activo = true;
                _documentoFuenteRepository.Update(existente);
                await _unitOfWork.SaveChangesAsync();
            }
            return;
        }

        await _documentoFuenteRepository.AddAsync(new DocumentoFuente
        {
            IdDocumento = idDocumento,
            IdFuente = idFuente,
            Activo = true
        });
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Documento {IdDocumento} vinculado automáticamente a la fuente RAG {IdFuente} (categoría {IdCategoria}).", idDocumento, idFuente, idCategoria);
    }
}
