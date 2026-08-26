using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class VistaAutorizadaRepository : IVistaAutorizadaRepository
{
    private readonly AsistenteDbContext _context;

    public VistaAutorizadaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<VistaAutorizada?> GetByIdAsync(int id)
    {
        return await _context.VistasAutorizadas
            .FirstOrDefaultAsync(v => v.IdVista == id);
    }

    public async Task<IEnumerable<VistaAutorizada>> GetByConexionIdAsync(int idConexion)
    {
        return await _context.VistasAutorizadas
            .AsNoTracking()
            .Where(v => v.IdConexion == idConexion)
            .OrderBy(v => v.NombreVista)
            .ToListAsync();
    }

    public async Task<IEnumerable<VistaAutorizada>> GetActivasByConexionIdAsync(int idConexion)
    {
        return await _context.VistasAutorizadas
            .AsNoTracking()
            .Where(v => v.IdConexion == idConexion && v.Activa)
            .OrderBy(v => v.NombreVista)
            .ToListAsync();
    }

    public async Task AddAsync(VistaAutorizada vista)
    {
        await _context.VistasAutorizadas.AddAsync(vista);
    }

    public void Update(VistaAutorizada vista)
    {
        _context.VistasAutorizadas.Update(vista);
    }

    public void Delete(VistaAutorizada vista)
    {
        _context.VistasAutorizadas.Remove(vista);
    }
}
