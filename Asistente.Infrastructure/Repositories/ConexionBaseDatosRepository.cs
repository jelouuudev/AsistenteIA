using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConexionBaseDatosRepository : IConexionBaseDatosRepository
{
    private readonly AsistenteDbContext _context;

    public ConexionBaseDatosRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<ConexionBaseDatos?> GetByIdAsync(int id)
    {
        return await _context.ConexionesBaseDatos
            .Include(c => c.TablasAutorizadas)
            .Include(c => c.VistasAutorizadas)
            .FirstOrDefaultAsync(c => c.IdConexion == id);
    }

    public async Task<IEnumerable<ConexionBaseDatos>> GetAllAsync()
    {
        return await _context.ConexionesBaseDatos
            .AsNoTracking()
            .OrderByDescending(c => c.FechaRegistro)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConexionBaseDatos>> GetActivasAsync()
    {
        return await _context.ConexionesBaseDatos
            .AsNoTracking()
            .Include(c => c.TablasAutorizadas)
            .Include(c => c.VistasAutorizadas)
            .Where(c => c.Activa)
            .OrderBy(c => c.Nombre)
            .ToListAsync();
    }

    public async Task AddAsync(ConexionBaseDatos conexion)
    {
        await _context.ConexionesBaseDatos.AddAsync(conexion);
    }

    public void Update(ConexionBaseDatos conexion)
    {
        _context.ConexionesBaseDatos.Update(conexion);
    }

    public void Delete(ConexionBaseDatos conexion)
    {
        _context.ConexionesBaseDatos.Remove(conexion);
    }
}
