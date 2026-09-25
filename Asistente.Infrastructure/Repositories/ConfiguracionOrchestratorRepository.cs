using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConfiguracionOrchestratorRepository : IConfiguracionOrchestratorRepository
{
    private readonly AsistenteDbContext _context;

    public ConfiguracionOrchestratorRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracionOrchestrator?> GetAsync()
        => await _context.ConfiguracionesOrchestrator.FirstOrDefaultAsync();

    public async Task UpdateAsync(ConfiguracionOrchestrator config)
    {
        var existente = await _context.ConfiguracionesOrchestrator.FirstOrDefaultAsync();
        if (existente == null)
        {
            config.IdConfiguracion = 1;
            await _context.ConfiguracionesOrchestrator.AddAsync(config);
        }
        else
        {
            existente.Habilitado = config.Habilitado;
            existente.Prioridad = config.Prioridad;
            existente.TiempoMaximoEjecucionMs = config.TiempoMaximoEjecucionMs;
            existente.MaxEjecucionesSimultaneas = config.MaxEjecucionesSimultaneas;
            existente.RequiereAutorizacion = config.RequiereAutorizacion;
            existente.MaxTiempoTotalMs = config.MaxTiempoTotalMs;
            existente.FechaActualizacion = System.DateTime.UtcNow;
        }
    }
}
