using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class DocumentoVersionRepository : IDocumentoVersionRepository
{
    private readonly AsistenteDbContext _context;

    public DocumentoVersionRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<DocumentoVersion?> GetByIdAsync(int id)
    {
        return await _context.Set<DocumentoVersion>()
            .FirstOrDefaultAsync(v => v.IdVersion == id);
    }

    public async Task<IEnumerable<DocumentoVersion>> GetByDocumentoIdAsync(int documentoId)
    {
        return await _context.Set<DocumentoVersion>()
            .Where(v => v.IdDocumento == documentoId)
            .OrderByDescending(v => v.NumeroVersion)
            .ToListAsync();
    }

    public async Task<DocumentoVersion?> GetVersionActivaAsync(int documentoId)
    {
        return await _context.Set<DocumentoVersion>()
            .Where(v => v.IdDocumento == documentoId && v.Activo)
            .OrderByDescending(v => v.NumeroVersion)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(DocumentoVersion version)
    {
        await _context.Set<DocumentoVersion>().AddAsync(version);
    }

    public void Update(DocumentoVersion version)
    {
        _context.Set<DocumentoVersion>().Update(version);
    }
}
