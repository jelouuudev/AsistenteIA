using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConfiguracionMemoriaRepository : IConfiguracionMemoriaRepository
{
    private readonly AsistenteDbContext _context;

    public ConfiguracionMemoriaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracionMemoria?> GetActivaAsync()
    {
        return await _context.ConfiguracionesMemoria
            .FirstOrDefaultAsync(c => c.Activo);
    }

    public async Task<ConfiguracionMemoria> CreateAsync(ConfiguracionMemoria configuracion)
    {
        _context.ConfiguracionesMemoria.Add(configuracion);
        return configuracion;
    }

    public async Task UpdateAsync(ConfiguracionMemoria configuracion)
    {
        _context.ConfiguracionesMemoria.Update(configuracion);
    }

    public async Task<IEnumerable<ConfiguracionMemoria>> GetAllAsync()
    {
        return await _context.ConfiguracionesMemoria
            .OrderByDescending(c => c.Activo)
            .ToListAsync();
    }

    public async Task<ConfiguracionMemoria?> GetByIdAsync(int id)
    {
        return await _context.ConfiguracionesMemoria
            .FirstOrDefaultAsync(c => c.IdConfiguracion == id);
    }
}
