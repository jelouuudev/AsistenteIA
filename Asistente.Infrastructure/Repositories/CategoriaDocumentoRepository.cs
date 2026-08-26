using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class CategoriaDocumentoRepository : ICategoriaDocumentoRepository
{
    private readonly AsistenteDbContext _context;

    public CategoriaDocumentoRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<CategoriaDocumento?> GetByIdAsync(int id)
    {
        return await _context.Set<CategoriaDocumento>()
            .Include(c => c.Documentos)
            .FirstOrDefaultAsync(c => c.IdCategoria == id);
    }

    public async Task<CategoriaDocumento?> GetByNombreAsync(string nombre)
    {
        return await _context.Set<CategoriaDocumento>()
            .FirstOrDefaultAsync(c => c.Nombre.ToLower() == nombre.ToLower());
    }

    public async Task<IEnumerable<CategoriaDocumento>> GetAllAsync()
    {
        return await _context.Set<CategoriaDocumento>()
            .Include(c => c.Documentos)
            .ToListAsync();
    }

    public async Task<IEnumerable<CategoriaDocumento>> GetAllActivasAsync()
    {
        return await _context.Set<CategoriaDocumento>()
            .Where(c => c.Activo)
            .ToListAsync();
    }

    public async Task AddAsync(CategoriaDocumento categoria)
    {
        await _context.Set<CategoriaDocumento>().AddAsync(categoria);
    }

    public void Update(CategoriaDocumento categoria)
    {
        _context.Set<CategoriaDocumento>().Update(categoria);
    }
}
