using System;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class ConfiguracionMotorConsultasService : IConfiguracionMotorConsultasService
{
    private readonly IConfiguracionMotorConsultasRepository _configRepository;
    private readonly IConexionBaseDatosRepository _conexionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfiguracionMotorConsultasService> _logger;

    public ConfiguracionMotorConsultasService(
        IConfiguracionMotorConsultasRepository configRepository,
        IConexionBaseDatosRepository conexionRepository,
        IUnitOfWork unitOfWork,
        ILogger<ConfiguracionMotorConsultasService> logger)
    {
        _configRepository = configRepository;
        _conexionRepository = conexionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ConfiguracionMotorConsultasDto?> ObtenerActivaAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetActivaAsync();
        if (config == null) return null;

        return await MapearADto(config);
    }

    public async Task<ConfiguracionMotorConsultasDto> ActualizarAsync(ActualizarConfiguracionMotorConsultasRequest request, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetActivaAsync();
        var existe = config != null;
        if (config == null)
        {
            config = new ConfiguracionMotorConsultas();
            await _configRepository.AddAsync(config);
        }

        config.TiempoMaximoEjecucionSegundos = request.TiempoMaximoEjecucionSegundos;
        config.MaximoRegistros = request.MaximoRegistros;
        config.MaxConsultasSimultaneas = request.MaxConsultasSimultaneas;
        config.IdConexionPredeterminada = request.IdConexionPredeterminada;
        config.Activo = request.Activo;

        if (existe)
        {
            _configRepository.Update(config);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Configuración del motor de consultas actualizada (Id {Id}).", config.IdConfiguracion);
        return (await MapearADto(config))!;
    }

    private async Task<ConfiguracionMotorConsultasDto> MapearADto(ConfiguracionMotorConsultas config)
    {
        string? nombreConexion = null;
        if (config.IdConexionPredeterminada.HasValue)
        {
            var conexion = await _conexionRepository.GetByIdAsync(config.IdConexionPredeterminada.Value);
            nombreConexion = conexion?.Nombre;
        }

        return new ConfiguracionMotorConsultasDto
        {
            IdConfiguracion = config.IdConfiguracion,
            TiempoMaximoEjecucionSegundos = config.TiempoMaximoEjecucionSegundos,
            MaximoRegistros = config.MaximoRegistros,
            MaxConsultasSimultaneas = config.MaxConsultasSimultaneas,
            IdConexionPredeterminada = config.IdConexionPredeterminada,
            NombreConexionPredeterminada = nombreConexion,
            Activo = config.Activo
        };
    }
}
