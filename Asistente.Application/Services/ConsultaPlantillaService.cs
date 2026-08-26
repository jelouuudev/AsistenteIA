using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class ConsultaPlantillaService : IConsultaPlantillaService
{
    private readonly IConsultaPlantillaRepository _plantillaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConsultaPlantillaService> _logger;

    public ConsultaPlantillaService(
        IConsultaPlantillaRepository plantillaRepository,
        IUnitOfWork unitOfWork,
        ILogger<ConsultaPlantillaService> logger)
    {
        _plantillaRepository = plantillaRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<ConsultaPlantillaDto>> ObtenerTodasAsync(CancellationToken cancellationToken = default)
    {
        var plantillas = await _plantillaRepository.GetAllAsync();
        return plantillas.Select(MapearADto);
    }

    public async Task<IEnumerable<ConsultaPlantillaDto>> ObtenerActivasAsync(CancellationToken cancellationToken = default)
    {
        var plantillas = await _plantillaRepository.GetActivasAsync();
        return plantillas.Select(MapearADto);
    }

    public async Task<ConsultaPlantillaDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var plantilla = await _plantillaRepository.GetByIdAsync(id);
        return plantilla == null ? null : MapearADto(plantilla);
    }

    public async Task<ConsultaPlantillaDto> CrearAsync(CrearConsultaPlantillaRequest request, CancellationToken cancellationToken = default)
    {
        var plantilla = new ConsultaPlantilla
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            IdConexion = request.IdConexion,
            ConsultaSql = request.ConsultaSql,
            Parametros = request.Parametros,
            Activa = true,
            FechaCreacion = DateTime.UtcNow
        };

        await _plantillaRepository.AddAsync(plantilla);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plantilla de consulta '{Nombre}' creada con ID {Id}.", plantilla.Nombre, plantilla.IdPlantilla);
        return (await ObtenerPorIdAsync(plantilla.IdPlantilla, cancellationToken))!;
    }

    public async Task<ConsultaPlantillaDto> ActualizarAsync(int id, ActualizarConsultaPlantillaRequest request, CancellationToken cancellationToken = default)
    {
        var plantilla = await _plantillaRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Plantilla con ID {id} no encontrada.");

        plantilla.Nombre = request.Nombre;
        plantilla.Descripcion = request.Descripcion;
        plantilla.IdConexion = request.IdConexion;
        plantilla.ConsultaSql = request.ConsultaSql;
        plantilla.Parametros = request.Parametros;
        plantilla.Activa = request.Activa;

        _plantillaRepository.Update(plantilla);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Plantilla '{Nombre}' actualizada.", plantilla.Nombre);
        return (await ObtenerPorIdAsync(id, cancellationToken))!;
    }

    public async Task ActivarAsync(int id, CancellationToken cancellationToken = default)
    {
        var plantilla = await _plantillaRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Plantilla con ID {id} no encontrada.");

        plantilla.Activa = true;
        _plantillaRepository.Update(plantilla);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DesactivarAsync(int id, CancellationToken cancellationToken = default)
    {
        var plantilla = await _plantillaRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Plantilla con ID {id} no encontrada.");

        plantilla.Activa = false;
        _plantillaRepository.Update(plantilla);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        var plantilla = await _plantillaRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Plantilla con ID {id} no encontrada.");

        _plantillaRepository.Delete(plantilla);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Plantilla {Id} eliminada.", id);
    }

    private static ConsultaPlantillaDto MapearADto(ConsultaPlantilla p)
    {
        return new ConsultaPlantillaDto
        {
            IdPlantilla = p.IdPlantilla,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            IdConexion = p.IdConexion,
            NombreConexion = p.Conexion?.Nombre,
            ConsultaSql = p.ConsultaSql,
            Parametros = p.Parametros,
            Activa = p.Activa,
            FechaCreacion = p.FechaCreacion
        };
    }
}
