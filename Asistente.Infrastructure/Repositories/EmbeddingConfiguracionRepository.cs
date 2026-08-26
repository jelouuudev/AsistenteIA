using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class EmbeddingConfiguracionRepository : IEmbeddingConfiguracionRepository
{
    private readonly AsistenteDbContext _context;

    public EmbeddingConfiguracionRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<EmbeddingConfiguracion?> GetActivaAsync()
    {
        return await _context.ConfiguracionesEmbedding
            .FirstOrDefaultAsync(c => c.Activo);
    }

    public async Task<EmbeddingConfiguracion?> GetByIdAsync(int id)
    {
        return await _context.ConfiguracionesEmbedding
            .FirstOrDefaultAsync(c => c.IdConfiguracion == id);
    }

    public async Task<IEnumerable<EmbeddingConfiguracion>> GetAllAsync()
    {
        return await _context.ConfiguracionesEmbedding
            .OrderByDescending(c => c.Activo)
            .ThenBy(c => c.IdConfiguracion)
            .ToListAsync();
    }

    public async Task AddAsync(EmbeddingConfiguracion configuracion)
    {
        await _context.ConfiguracionesEmbedding.AddAsync(configuracion);
    }

    public void Update(EmbeddingConfiguracion configuracion)
    {
        _context.ConfiguracionesEmbedding.Update(configuracion);
    }
}
