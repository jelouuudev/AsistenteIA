using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class RolService : IRolService
{
    private readonly IRolRepository _rolRepository;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IUnitOfWork _unitOfWork;

    public RolService(IRolRepository rolRepository, IAuditoriaService auditoriaService, IUnitOfWork unitOfWork)
    {
        _rolRepository = rolRepository;
        _auditoriaService = auditoriaService;
        _unitOfWork = unitOfWork;
    }

    public async Task<RolDto?> ObtenerPorIdAsync(int id)
    {
        var rol = await _rolRepository.GetByIdAsync(id);
        if (rol == null) return null;

        return MapToDto(rol);
    }

    public async Task<RolDto?> ObtenerPorNombreAsync(string nombre)
    {
        var rol = await _rolRepository.GetByNombreAsync(nombre);
        if (rol == null) return null;

        return MapToDto(rol);
    }

    public async Task<IEnumerable<RolDto>> ObtenerTodosAsync()
    {
        var roles = await _rolRepository.GetAllAsync();
        return roles.Select(MapToDto);
    }

    public async Task<RolDto> CrearRolAsync(CrearRolRequest request, int currentUserId, string ipAddress)
    {
        var existente = await _rolRepository.GetByNombreAsync(request.Nombre);
        if (existente != null)
        {
            throw new InvalidOperationException($"El rol '{request.Nombre}' ya existe.");
        }

        var rol = new Rol
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Activo = true
        };

        await _rolRepository.AddAsync(rol);
        await _unitOfWork.SaveChangesAsync();

        await _auditoriaService.RegistrarActividadAsync(
            currentUserId,
            "Roles",
            "Creación",
            $"Se creó el rol '{rol.Nombre}'",
            ipAddress);

        return MapToDto(rol);
    }

    public async Task<RolDto> ActualizarRolAsync(int id, ActualizarRolRequest request, int currentUserId, string ipAddress)
    {
        var rol = await _rolRepository.GetByIdAsync(id);
        if (rol == null)
        {
            throw new KeyNotFoundException("Rol no encontrado.");
        }

        var oldNombre = rol.Nombre;
        var oldActivo = rol.Activo;
        var oldDescripcion = rol.Descripcion;

        rol.Nombre = request.Nombre;
        rol.Descripcion = request.Descripcion;
        rol.Activo = request.Activo;

        _rolRepository.Update(rol);
        await _unitOfWork.SaveChangesAsync();

        var cambios = new List<string>();
        if (oldNombre != request.Nombre) cambios.Add($"Nombre a '{request.Nombre}'");
        if (oldDescripcion != request.Descripcion) cambios.Add($"Descripción a '{request.Descripcion}'");
        if (oldActivo != request.Activo) cambios.Add($"Activo a '{request.Activo}'");

        var desc = cambios.Any() ? string.Join(", ", cambios) : "Sin cambios significativos";

        await _auditoriaService.RegistrarActividadAsync(
            currentUserId,
            "Roles",
            "Modificación",
            $"Se modificó el rol '{oldNombre}': {desc}",
            ipAddress);

        return MapToDto(rol);
    }

    private static RolDto MapToDto(Rol rol)
    {
        return new RolDto
        {
            IdRol = rol.IdRol,
            Nombre = rol.Nombre,
            Descripcion = rol.Descripcion,
            Activo = rol.Activo
        };
    }
}
