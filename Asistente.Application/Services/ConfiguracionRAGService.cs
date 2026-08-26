using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class ConfiguracionRAGService : IConfiguracionRAGService
{
    private readonly IConfiguracionRAGRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfiguracionRAGService> _logger;

    public ConfiguracionRAGService(
        IConfiguracionRAGRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ConfiguracionRAGService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ConfiguracionRAGDto> ObtenerActivaAsync()
    {
        var config = await _repository.GetActivaAsync();
        if (config == null)
        {
            var porDefecto = new ConfiguracionRAG();
            return MapearADto(porDefecto);
        }
        return MapearADto(config);
    }

    public async Task<ConfiguracionRAGDto> ActualizarAsync(ActualizarConfiguracionRAGRequest request)
    {
        var config = await _repository.GetActivaAsync();
        if (config == null)
        {
            config = new ConfiguracionRAG
            {
                MaxChunks = request.MaxChunks,
                MaxCaracteresContexto = request.MaxCaracteresContexto,
                MinScore = request.MinScore,
                TopKPorFuente = request.TopKPorFuente,
                MaxFuentesConsultadas = request.MaxFuentesConsultadas,
                MaxReferencias = request.MaxReferencias,
                MaxChunksAlModelo = request.MaxChunksAlModelo,
                UsarDocumentosHistoricos = request.UsarDocumentosHistoricos,
                Activo = true
            };
            await _repository.AddAsync(config);
        }
        else
        {
            config.MaxChunks = request.MaxChunks;
            config.MaxCaracteresContexto = request.MaxCaracteresContexto;
            config.MinScore = request.MinScore;
            config.TopKPorFuente = request.TopKPorFuente;
            config.MaxFuentesConsultadas = request.MaxFuentesConsultadas;
            config.MaxReferencias = request.MaxReferencias;
            config.MaxChunksAlModelo = request.MaxChunksAlModelo;
            config.UsarDocumentosHistoricos = request.UsarDocumentosHistoricos;
            _repository.Update(config);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Configuracion RAG actualizada.");
        return MapearADto(config);
    }

    private static ConfiguracionRAGDto MapearADto(ConfiguracionRAG config)
    {
        return new ConfiguracionRAGDto
        {
            IdConfiguracion = config.IdConfiguracion,
            MaxChunks = config.MaxChunks,
            MaxCaracteresContexto = config.MaxCaracteresContexto,
            MinScore = config.MinScore,
            TopKPorFuente = config.TopKPorFuente,
            MaxFuentesConsultadas = config.MaxFuentesConsultadas,
            MaxReferencias = config.MaxReferencias,
            MaxChunksAlModelo = config.MaxChunksAlModelo,
            UsarDocumentosHistoricos = config.UsarDocumentosHistoricos,
            Activo = config.Activo
        };
    }
}
