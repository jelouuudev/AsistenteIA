using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Eventos;

public class ConfiguracionEventoMotorService : IConfiguracionEventoMotorService
{
    private readonly IConfiguracionEventoMotorRepository _repository;

    public ConfiguracionEventoMotorService(IConfiguracionEventoMotorRepository repository) => _repository = repository;

    public async Task<ConfiguracionEventoMotorDto> ObtenerAsync(CancellationToken ct = default)
    {
        var c = await _repository.GetAsync(ct);
        return new ConfiguracionEventoMotorDto
        {
            ReintentosMaximos = c.ReintentosMaximos,
            IntervaloReintentoMs = c.IntervaloReintentoMs,
            TiempoMaximoEventoMs = c.TiempoMaximoEventoMs,
            EventosSimultaneosMax = c.EventosSimultaneosMax,
            FrecuenciaProcesadorMs = c.FrecuenciaProcesadorMs
        };
    }

    public async Task GuardarAsync(ConfiguracionEventoMotorDto config, CancellationToken ct = default)
    {
        var c = await _repository.GetAsync(ct) ?? new ConfiguracionEventoMotor { IdConfiguracion = 1 };
        c.ReintentosMaximos = config.ReintentosMaximos;
        c.IntervaloReintentoMs = config.IntervaloReintentoMs;
        c.TiempoMaximoEventoMs = config.TiempoMaximoEventoMs;
        c.EventosSimultaneosMax = config.EventosSimultaneosMax;
        c.FrecuenciaProcesadorMs = config.FrecuenciaProcesadorMs;
        await _repository.UpdateAsync(c, ct);
    }
}
