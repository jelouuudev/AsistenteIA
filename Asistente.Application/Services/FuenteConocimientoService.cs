using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class FuenteConocimientoService : IFuenteConocimientoService
{
    private readonly IFuenteConocimientoRepository _fuenteRepository;
    private readonly IAsistenteFuenteRepository _asistenteFuenteRepository;
    private readonly IDocumentoFuenteRepository _documentoFuenteRepository;
    private readonly IVectorStore _vectorStore;
    private readonly IProcesamientoDocumentalRepository _procesamientoRepository;
    private readonly IDocumentoIndexadoRepository _indexadoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIndexacionService _indexacionService;
    private readonly ILogger<FuenteConocimientoService> _logger;

    public FuenteConocimientoService(
        IFuenteConocimientoRepository fuenteRepository,
        IAsistenteFuenteRepository asistenteFuenteRepository,
        IDocumentoFuenteRepository documentoFuenteRepository,
        IVectorStore vectorStore,
        IProcesamientoDocumentalRepository procesamientoRepository,
        IDocumentoIndexadoRepository indexadoRepository,
        IUnitOfWork unitOfWork,
        IIndexacionService indexacionService,
        ILogger<FuenteConocimientoService> logger)
    {
        _fuenteRepository = fuenteRepository;
        _asistenteFuenteRepository = asistenteFuenteRepository;
        _documentoFuenteRepository = documentoFuenteRepository;
        _vectorStore = vectorStore;
        _procesamientoRepository = procesamientoRepository;
        _indexadoRepository = indexadoRepository;
        _unitOfWork = unitOfWork;
        _indexacionService = indexacionService;
        _logger = logger;
    }

    public async Task<IEnumerable<FuenteConocimientoDto>> ObtenerTodasAsync()
    {
        var fuentes = await _fuenteRepository.GetAllAsync();
        return fuentes.Select(f => MapearADto(f));
    }

    public async Task<IEnumerable<FuenteConocimientoDto>> ObtenerActivasAsync()
    {
        var fuentes = await _fuenteRepository.GetActivasAsync();
        return fuentes.Select(f => MapearADto(f));
    }

    public async Task<FuenteConocimientoDto?> ObtenerPorIdAsync(int id)
    {
        var fuente = await _fuenteRepository.GetByIdAsync(id);
        if (fuente == null) return null;
        return MapearADto(fuente);
    }

    public async Task<FuenteConocimientoDto> CrearAsync(CrearFuenteConocimientoRequest request, int usuarioCreacion)
    {
        var tipo = Enum.TryParse<TipoFuente>(request.Tipo, true, out var t) ? t : TipoFuente.Manual;

        var fuente = new FuenteConocimiento
        {
            Nombre = request.Nombre,
            Codigo = request.Codigo,
            Descripcion = request.Descripcion,
            Tipo = tipo,
            Prioridad = request.Prioridad,
            Activo = true,
            UsuarioCreacion = usuarioCreacion,
            FechaCreacion = DateTime.UtcNow
        };

        await _fuenteRepository.AddAsync(fuente);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Fuente de conocimiento '{Nombre}' creada con ID {Id}.", fuente.Nombre, fuente.IdFuente);
        return MapearADto(fuente);
    }

    public async Task<FuenteConocimientoDto> ActualizarAsync(int id, ActualizarFuenteConocimientoRequest request)
    {
        var fuente = await _fuenteRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Fuente de conocimiento con ID {id} no encontrada.");

        var tipo = Enum.TryParse<TipoFuente>(request.Tipo, true, out var t) ? t : TipoFuente.Manual;

        fuente.Nombre = request.Nombre;
        fuente.Codigo = request.Codigo;
        fuente.Descripcion = request.Descripcion;
        fuente.Tipo = tipo;
        fuente.Activo = request.Activo;
        fuente.Prioridad = request.Prioridad;

        _fuenteRepository.Update(fuente);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Fuente de conocimiento '{Nombre}' actualizada.", fuente.Nombre);
        return MapearADto(fuente);
    }

    public async Task ActivarAsync(int id)
    {
        var fuente = await _fuenteRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Fuente de conocimiento con ID {id} no encontrada.");

        fuente.Activo = true;
        _fuenteRepository.Update(fuente);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DesactivarAsync(int id)
    {
        var fuente = await _fuenteRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Fuente de conocimiento con ID {id} no encontrada.");

        fuente.Activo = false;
        _fuenteRepository.Update(fuente);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<DashboardFuentesDto> ObtenerDashboardAsync()
    {
        var fuentes = (await _fuenteRepository.GetAllAsync()).ToList();
        var totalChunksGlobal = await _vectorStore.GetDocumentCountAsync();
        var todosIndexados = (await _indexadoRepository.GetAllAsync()).ToList();
        var todosProcesados = (await _procesamientoRepository.GetAllAsync()).ToList();

        DateTime? fechaGlobal = todosIndexados
            .Where(i => i.Estado == Domain.Enums.EstadoIndexacion.Indexado)
            .Max(i => (DateTime?)i.FechaIndexacion);

        var fuentesDto = new List<FuenteConocimientoDto>();

        foreach (var fuente in fuentes)
        {
            var docsProcesadosIds = await _documentoFuenteRepository.GetDocumentosProcesadosIdsByFuenteAsync(fuente.IdFuente);
            var idsSet = docsProcesadosIds.ToHashSet();

            var chunksFuente = todosProcesados
                .Where(p => idsSet.Contains(p.IdDocumentoProcesado))
                .Sum(p => p.TotalChunks);

            var indexadosFuente = todosIndexados
                .Where(i => idsSet.Contains(i.IdDocumentoProcesado) && i.Estado == Domain.Enums.EstadoIndexacion.Indexado)
                .ToList();

            var ultimaIndexacion = indexadosFuente.Max(i => (DateTime?)i.FechaIndexacion);

            fuentesDto.Add(new FuenteConocimientoDto
            {
                IdFuente = fuente.IdFuente,
                Nombre = fuente.Nombre,
                Codigo = fuente.Codigo,
                Descripcion = fuente.Descripcion,
                Tipo = fuente.Tipo.ToString(),
                Activo = fuente.Activo,
                Prioridad = fuente.Prioridad,
                FechaCreacion = fuente.FechaCreacion,
                UsuarioCreacion = fuente.UsuarioCreacion,
                TotalDocumentos = fuente.DocumentosFuentes?.Count ?? 0,
                TotalAsistentes = fuente.AsistentesFuentes?.Count ?? 0,
                TotalChunks = chunksFuente,
                TotalVectores = indexadosFuente.Sum(i => i.TotalEmbeddings),
                FechaUltimaIndexacion = ultimaIndexacion
            });
        }

        return new DashboardFuentesDto
        {
            TotalFuentes = fuentes.Count,
            TotalFuentesActivas = fuentes.Count(f => f.Activo),
            TotalDocumentosAsociados = fuentes.Sum(f => f.DocumentosFuentes?.Count ?? 0),
            TotalAsistentesConFuentes = fuentes.SelectMany(f => f.AsistentesFuentes ?? Enumerable.Empty<AsistenteFuente>()).Select(af => af.IdAsistente).Distinct().Count(),
            TotalChunks = totalChunksGlobal,
            TotalVectores = todosIndexados.Where(i => i.Estado == Domain.Enums.EstadoIndexacion.Indexado).Sum(i => i.TotalEmbeddings),
            FechaUltimaIndexacionGlobal = fechaGlobal,
            Fuentes = fuentesDto
        };
    }

    public async Task<IEnumerable<AsistenteFuenteDto>> ObtenerFuentesDeAsistenteAsync(int idAsistente)
    {
        var relaciones = await _asistenteFuenteRepository.GetByAsistenteIdAsync(idAsistente);
        return relaciones.Select(af => new AsistenteFuenteDto
        {
            IdAsistente = af.IdAsistente,
            IdFuente = af.IdFuente,
            NombreAsistente = af.Asistente?.Nombre,
            NombreFuente = af.Fuente?.Nombre,
            Activo = af.Activo,
            Prioridad = af.Prioridad
        });
    }

    public async Task<IEnumerable<AsistenteFuenteDto>> ObtenerAsistentesDeFuenteAsync(int idFuente)
    {
        var relaciones = await _asistenteFuenteRepository.GetByFuenteIdAsync(idFuente);
        return relaciones.Select(af => new AsistenteFuenteDto
        {
            IdAsistente = af.IdAsistente,
            IdFuente = af.IdFuente,
            NombreAsistente = af.Asistente?.Nombre,
            NombreFuente = af.Fuente?.Nombre,
            Activo = af.Activo,
            Prioridad = af.Prioridad
        });
    }

    public async Task AsignarFuenteAAsistenteAsync(AsignarFuenteAAsistenteRequest request)
    {
        var existente = await _asistenteFuenteRepository.GetByClaveAsync(request.IdAsistente, request.IdFuente);
        if (existente != null)
        {
            existente.Activo = true;
            existente.Prioridad = request.Prioridad;
            _asistenteFuenteRepository.Update(existente);
        }
        else
        {
            var nueva = new AsistenteFuente
            {
                IdAsistente = request.IdAsistente,
                IdFuente = request.IdFuente,
                Activo = true,
                Prioridad = request.Prioridad
            };
            await _asistenteFuenteRepository.AddAsync(nueva);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Fuente {IdFuente} asignada al asistente {IdAsistente}.", request.IdFuente, request.IdAsistente);
    }

    public async Task DesasignarFuenteDeAsistenteAsync(int idAsistente, int idFuente)
    {
        var relacion = await _asistenteFuenteRepository.GetByClaveAsync(idAsistente, idFuente);
        if (relacion != null)
        {
            _asistenteFuenteRepository.Delete(relacion);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task ActivarAsistenteFuenteAsync(int idAsistente, int idFuente)
    {
        var relacion = await _asistenteFuenteRepository.GetByClaveAsync(idAsistente, idFuente)
            ?? throw new KeyNotFoundException("La relación asistente-fuente no existe.");

        relacion.Activo = true;
        _asistenteFuenteRepository.Update(relacion);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DesactivarAsistenteFuenteAsync(int idAsistente, int idFuente)
    {
        var relacion = await _asistenteFuenteRepository.GetByClaveAsync(idAsistente, idFuente)
            ?? throw new KeyNotFoundException("La relación asistente-fuente no existe.");

        relacion.Activo = false;
        _asistenteFuenteRepository.Update(relacion);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<DocumentoFuenteDto>> ObtenerDocumentosDeFuenteAsync(int idFuente)
    {
        var relaciones = await _documentoFuenteRepository.GetByFuenteIdAsync(idFuente);
        return relaciones.Select(df => new DocumentoFuenteDto
        {
            IdDocumento = df.IdDocumento,
            IdFuente = df.IdFuente,
            NombreDocumento = df.Documento?.Nombre,
            NombreFuente = df.Fuente?.Nombre,
            Activo = df.Activo
        });
    }

    public async Task<IEnumerable<FuenteConocimientoDto>> ObtenerFuentesDeDocumentoAsync(int idDocumento)
    {
        var relaciones = await _documentoFuenteRepository.GetByDocumentoIdAsync(idDocumento);
        return relaciones
            .Where(df => df.Fuente != null)
            .Select(df => MapearADto(df.Fuente!));
    }

    public async Task AsignarDocumentoAFuenteAsync(AsignarDocumentoAFuenteRequest request)
    {
        var existente = await _documentoFuenteRepository.GetByClaveAsync(request.IdDocumento, request.IdFuente);
        if (existente != null)
        {
            existente.Activo = true;
            _documentoFuenteRepository.Update(existente);
        }
        else
        {
            var nueva = new DocumentoFuente
            {
                IdDocumento = request.IdDocumento,
                IdFuente = request.IdFuente,
                Activo = true
            };
            await _documentoFuenteRepository.AddAsync(nueva);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Documento {IdDoc} asignado a la fuente {IdFuente}.", request.IdDocumento, request.IdFuente);

        // SEGURIDAD: reindexar para que los vectores reflejen la asignacion vigente.
        try
        {
            await _indexacionService.ReindexarPorDocumentoAsync(request.IdDocumento);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo reindexar el documento {IdDoc} tras asignarlo a la fuente. Se actualizara en el proximo ciclo.", request.IdDocumento);
        }
    }

    public async Task DesasignarDocumentoDeFuenteAsync(int idDocumento, int idFuente)
    {
        var relacion = await _documentoFuenteRepository.GetByClaveAsync(idDocumento, idFuente);
        if (relacion != null)
        {
            _documentoFuenteRepository.Delete(relacion);
            await _unitOfWork.SaveChangesAsync();

            // SEGURIDAD: reindexar para eliminar vectores huerfanos de la fuente anterior.
            // Sin esto, el documento desasignado seguiria apareciendo en busquedas.
            try
            {
                await _indexacionService.ReindexarPorDocumentoAsync(idDocumento);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo reindexar el documento {IdDoc} tras desasignarlo de la fuente. Se actualizara en el proximo ciclo.", idDocumento);
            }
        }
    }

    private static FuenteConocimientoDto MapearADto(FuenteConocimiento fuente)
    {
        return new FuenteConocimientoDto
        {
            IdFuente = fuente.IdFuente,
            Nombre = fuente.Nombre,
            Codigo = fuente.Codigo,
            Descripcion = fuente.Descripcion,
            Tipo = fuente.Tipo.ToString(),
            Activo = fuente.Activo,
            Prioridad = fuente.Prioridad,
            FechaCreacion = fuente.FechaCreacion,
            UsuarioCreacion = fuente.UsuarioCreacion,
            TotalDocumentos = fuente.DocumentosFuentes?.Count ?? 0,
            TotalAsistentes = fuente.AsistentesFuentes?.Count ?? 0
        };
    }
}
