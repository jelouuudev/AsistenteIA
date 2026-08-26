using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class ConfiguracionMemoriaService : IConfiguracionMemoriaService
{
    private readonly IConfiguracionMemoriaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfiguracionMemoriaService(
        IConfiguracionMemoriaRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfiguracionMemoriaDto?> ObtenerActivaAsync()
    {
        var config = await _repository.GetActivaAsync();
        if (config == null) return null;

        return MapToDto(config);
    }

    public async Task<ConfiguracionMemoriaDto> ActualizarAsync(ActualizarConfiguracionMemoriaRequest request)
    {
        var config = await _repository.GetActivaAsync();

        if (config == null)
        {
            config = new ConfiguracionMemoria
            {
                MaximoMensajesContexto = request.MaximoMensajesContexto,
                MaximoTokensContexto = request.MaximoTokensContexto,
                LongitudResumen = request.LongitudResumen,
                CantidadConversacionesVisibles = request.CantidadConversacionesVisibles,
                Activo = request.Activo
            };
            await _repository.CreateAsync(config);
        }
        else
        {
            config.MaximoMensajesContexto = request.MaximoMensajesContexto;
            config.MaximoTokensContexto = request.MaximoTokensContexto;
            config.LongitudResumen = request.LongitudResumen;
            config.CantidadConversacionesVisibles = request.CantidadConversacionesVisibles;
            config.Activo = request.Activo;
            await _repository.UpdateAsync(config);
        }

        await _unitOfWork.SaveChangesAsync();
        return MapToDto(config);
    }

    private static ConfiguracionMemoriaDto MapToDto(ConfiguracionMemoria config)
    {
        return new ConfiguracionMemoriaDto
        {
            IdConfiguracion = config.IdConfiguracion,
            MaximoMensajesContexto = config.MaximoMensajesContexto,
            MaximoTokensContexto = config.MaximoTokensContexto,
            LongitudResumen = config.LongitudResumen,
            CantidadConversacionesVisibles = config.CantidadConversacionesVisibles,
            Activo = config.Activo
        };
    }
}
