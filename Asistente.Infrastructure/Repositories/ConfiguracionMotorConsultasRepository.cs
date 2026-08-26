using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConfiguracionMotorConsultasRepository : IConfiguracionMotorConsultasRepository
{
    private readonly AsistenteDbContext _context;

    public ConfiguracionMotorConsultasRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracionMotorConsultas?> GetActivaAsync()
    {
        return await _context.ConfiguracionesMotorConsultas
            .Where(c => c.Activo)
            .OrderByDescending(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();
    }

    public async Task<ConfiguracionMotorConsultas?> GetByIdAsync(int id)
    {
        return await _context.ConfiguracionesMotorConsultas
            .FirstOrDefaultAsync(c => c.IdConfiguracion == id);
    }

    public async Task AddAsync(ConfiguracionMotorConsultas config)
    {
        await _context.ConfiguracionesMotorConsultas.AddAsync(config);
    }

    public void Update(ConfiguracionMotorConsultas config)
    {
        _context.ConfiguracionesMotorConsultas.Update(config);
    }
}
