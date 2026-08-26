using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class EmbeddingConfiguracionService : IEmbeddingConfiguracionService
{
    private readonly IEmbeddingConfiguracionRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmbeddingConfiguracionService> _logger;

    public EmbeddingConfiguracionService(
        IEmbeddingConfiguracionRepository configRepository,
        IUnitOfWork unitOfWork,
        ILogger<EmbeddingConfiguracionService> logger)
    {
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<EmbeddingConfiguracionDto?> ObtenerActivaAsync()
    {
        var config = await _configRepository.GetActivaAsync();
        if (config == null) return null;
        return MapToDto(config);
    }

    public async Task<EmbeddingConfiguracionDto?> ObtenerPorIdAsync(int id)
    {
        var config = await _configRepository.GetByIdAsync(id);
        if (config == null) return null;
        return MapToDto(config);
    }

    public async Task<IEnumerable<EmbeddingConfiguracionDto>> ObtenerTodasAsync()
    {
        var configs = await _configRepository.GetAllAsync();
        return configs.Select(MapToDto);
    }

    public async Task<EmbeddingConfiguracionDto> ActualizarAsync(int id, ActualizarEmbeddingConfiguracionRequest request)
    {
        var config = await _configRepository.GetByIdAsync(id);
        if (config == null)
            throw new KeyNotFoundException($"Configuración {id} no encontrada.");

        config.Proveedor = request.Proveedor;
        config.ModeloEmbeddings = request.ModeloEmbeddings;
        config.BaseVectorial = request.BaseVectorial;
        config.CantidadResultados = request.CantidadResultados;
        config.PuntajeMinimo = request.PuntajeMinimo;
        config.LongitudMaximaContexto = request.LongitudMaximaContexto;
        config.Activo = request.Activo;

        if (config.Activo)
        {
            var activas = await _configRepository.GetAllAsync();
            foreach (var otra in activas.Where(c => c.IdConfiguracion != id && c.Activo))
            {
                otra.Activo = false;
                _configRepository.Update(otra);
            }
        }

        _configRepository.Update(config);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Configuración de embeddings {Id} actualizada.", id);
        return MapToDto(config);
    }

    public async Task<EmbeddingConfiguracionDto> CrearAsync(ActualizarEmbeddingConfiguracionRequest request)
    {
        if (request.Activo)
        {
            var activa = await _configRepository.GetActivaAsync();
            if (activa != null)
            {
                activa.Activo = false;
                _configRepository.Update(activa);
            }
        }

        var nueva = new EmbeddingConfiguracion
        {
            Proveedor = request.Proveedor,
            ModeloEmbeddings = request.ModeloEmbeddings,
            BaseVectorial = request.BaseVectorial,
            CantidadResultados = request.CantidadResultados,
            PuntajeMinimo = request.PuntajeMinimo,
            LongitudMaximaContexto = request.LongitudMaximaContexto,
            Activo = request.Activo
        };

        await _configRepository.AddAsync(nueva);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Configuración de embeddings creada con ID {Id}.", nueva.IdConfiguracion);
        return MapToDto(nueva);
    }

    private static EmbeddingConfiguracionDto MapToDto(EmbeddingConfiguracion c)
    {
        return new EmbeddingConfiguracionDto
        {
            IdConfiguracion = c.IdConfiguracion,
            Proveedor = c.Proveedor,
            ModeloEmbeddings = c.ModeloEmbeddings,
            BaseVectorial = c.BaseVectorial,
            CantidadResultados = c.CantidadResultados,
            PuntajeMinimo = c.PuntajeMinimo,
            LongitudMaximaContexto = c.LongitudMaximaContexto,
            Activo = c.Activo
        };
    }
}
