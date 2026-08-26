using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class AuditoriaDocumentalRepository : IAuditoriaDocumentalRepository
{
    private readonly AsistenteDbContext _context;

    public AuditoriaDocumentalRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AuditoriaDocumental>> GetByDocumentoIdAsync(int documentoId)
    {
        return await _context.Set<AuditoriaDocumental>()
            .Include(a => a.Documento)
            .Where(a => a.IdDocumento == documentoId)
            .OrderByDescending(a => a.FechaAccion)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditoriaDocumental>> GetAllAsync()
    {
        return await _context.Set<AuditoriaDocumental>()
            .Include(a => a.Documento)
            .OrderByDescending(a => a.FechaAccion)
            .ToListAsync();
    }

    public async Task AddAsync(AuditoriaDocumental auditoria)
    {
        await _context.Set<AuditoriaDocumental>().AddAsync(auditoria);
    }
}
