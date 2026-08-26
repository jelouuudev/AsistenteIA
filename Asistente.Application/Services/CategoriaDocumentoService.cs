using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class CategoriaDocumentoService : ICategoriaDocumentoService
{
    private readonly ICategoriaDocumentoRepository _categoriaRepository;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IUnitOfWork _unitOfWork;

    public CategoriaDocumentoService(
        ICategoriaDocumentoRepository categoriaRepository,
        IAuditoriaService auditoriaService,
        IUnitOfWork unitOfWork)
    {
        _categoriaRepository = categoriaRepository;
        _auditoriaService = auditoriaService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CategoriaDocumentoDto?> ObtenerPorIdAsync(int id)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id);
        if (categoria == null) return null;
        return MapToDto(categoria);
    }

    public async Task<IEnumerable<CategoriaDocumentoDto>> ObtenerTodasAsync()
    {
        var categorias = await _categoriaRepository.GetAllAsync();
        return categorias.Select(MapToDto);
    }

    public async Task<IEnumerable<CategoriaDocumentoDto>> ObtenerActivasAsync()
    {
        var categorias = await _categoriaRepository.GetAllActivasAsync();
        return categorias.Select(MapToDto);
    }

    public async Task<CategoriaDocumentoDto> CrearAsync(CrearCategoriaDocumentoRequest request, int currentUserId, string ipAddress)
    {
        var existente = await _categoriaRepository.GetByNombreAsync(request.Nombre);
        if (existente != null)
        {
            throw new InvalidOperationException($"La categoría '{request.Nombre}' ya existe.");
        }

        var categoria = new CategoriaDocumento
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Activo = true
        };

        await _categoriaRepository.AddAsync(categoria);
        await _unitOfWork.SaveChangesAsync();

        await _auditoriaService.RegistrarActividadAsync(
            currentUserId,
            "GestorDocumental",
            "CrearCategoria",
            $"Se creó la categoría documental '{categoria.Nombre}'",
            ipAddress);

        return MapToDto(categoria);
    }

    public async Task<CategoriaDocumentoDto> ActualizarAsync(int id, ActualizarCategoriaDocumentoRequest request, int currentUserId, string ipAddress)
    {
        var categoria = await _categoriaRepository.GetByIdAsync(id);
        if (categoria == null)
        {
            throw new KeyNotFoundException("Categoría no encontrada.");
        }

        var oldNombre = categoria.Nombre;
        var oldActivo = categoria.Activo;

        categoria.Nombre = request.Nombre;
        categoria.Descripcion = request.Descripcion;
        categoria.Activo = request.Activo;

        _categoriaRepository.Update(categoria);
        await _unitOfWork.SaveChangesAsync();

        var cambios = new List<string>();
        if (oldNombre != request.Nombre) cambios.Add($"Nombre a '{request.Nombre}'");
        if (oldActivo != request.Activo) cambios.Add($"Activo a '{request.Activo}'");

        var desc = cambios.Any() ? string.Join(", ", cambios) : "Sin cambios significativos";

        await _auditoriaService.RegistrarActividadAsync(
            currentUserId,
            "GestorDocumental",
            "ModificarCategoria",
            $"Se modificó la categoría '{oldNombre}': {desc}",
            ipAddress);

        return MapToDto(categoria);
    }

    private static CategoriaDocumentoDto MapToDto(CategoriaDocumento categoria)
    {
        return new CategoriaDocumentoDto
        {
            IdCategoria = categoria.IdCategoria,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion,
            Activo = categoria.Activo,
            TotalDocumentos = categoria.Documentos?.Count ?? 0
        };
    }
}
