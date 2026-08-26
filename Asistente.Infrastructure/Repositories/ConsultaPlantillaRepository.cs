using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConsultaPlantillaRepository : IConsultaPlantillaRepository
{
    private readonly AsistenteDbContext _context;

    public ConsultaPlantillaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<ConsultaPlantilla?> GetByIdAsync(int id)
    {
        return await _context.ConsultasPlantillas
            .Include(p => p.Conexion)
            .FirstOrDefaultAsync(p => p.IdPlantilla == id);
    }

    public async Task<IEnumerable<ConsultaPlantilla>> GetAllAsync()
    {
        return await _context.ConsultasPlantillas
            .AsNoTracking()
            .Include(p => p.Conexion)
            .OrderBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultaPlantilla>> GetActivasAsync()
    {
        return await _context.ConsultasPlantillas
            .AsNoTracking()
            .Include(p => p.Conexion)
            .Where(p => p.Activa)
            .OrderBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultaPlantilla>> GetActivasByConexionIdAsync(int idConexion)
    {
        return await _context.ConsultasPlantillas
            .AsNoTracking()
            .Include(p => p.Conexion)
            .Where(p => p.Activa && (p.IdConexion == null || p.IdConexion == idConexion))
            .OrderBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task AddAsync(ConsultaPlantilla plantilla)
    {
        await _context.ConsultasPlantillas.AddAsync(plantilla);
    }

    public void Update(ConsultaPlantilla plantilla)
    {
        _context.ConsultasPlantillas.Update(plantilla);
    }

    public void Delete(ConsultaPlantilla plantilla)
    {
        _context.ConsultasPlantillas.Remove(plantilla);
    }
}
