using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConfiguracionRAGRepository : IConfiguracionRAGRepository
{
    private readonly AsistenteDbContext _context;

    public ConfiguracionRAGRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracionRAG?> GetActivaAsync()
    {
        return await _context.ConfiguracionesRAG
            .Where(c => c.Activo)
            .OrderByDescending(c => c.IdConfiguracion)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(ConfiguracionRAG configuracion)
    {
        await _context.ConfiguracionesRAG.AddAsync(configuracion);
    }

    public void Update(ConfiguracionRAG configuracion)
    {
        _context.ConfiguracionesRAG.Update(configuracion);
    }
}
