using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class TablaAutorizadaRepository : ITablaAutorizadaRepository
{
    private readonly AsistenteDbContext _context;

    public TablaAutorizadaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<TablaAutorizada?> GetByIdAsync(int id)
    {
        return await _context.TablasAutorizadas
            .FirstOrDefaultAsync(t => t.IdTabla == id);
    }

    public async Task<IEnumerable<TablaAutorizada>> GetByConexionIdAsync(int idConexion)
    {
        return await _context.TablasAutorizadas
            .AsNoTracking()
            .Where(t => t.IdConexion == idConexion)
            .OrderBy(t => t.Esquema)
            .ThenBy(t => t.NombreTabla)
            .ToListAsync();
    }

    public async Task<IEnumerable<TablaAutorizada>> GetActivasByConexionIdAsync(int idConexion)
    {
        return await _context.TablasAutorizadas
            .AsNoTracking()
            .Where(t => t.IdConexion == idConexion && t.Activa)
            .OrderBy(t => t.Esquema)
            .ThenBy(t => t.NombreTabla)
            .ToListAsync();
    }

    public async Task AddAsync(TablaAutorizada tabla)
    {
        await _context.TablasAutorizadas.AddAsync(tabla);
    }

    public void Update(TablaAutorizada tabla)
    {
        _context.TablasAutorizadas.Update(tabla);
    }

    public void Delete(TablaAutorizada tabla)
    {
        _context.TablasAutorizadas.Remove(tabla);
    }
}
