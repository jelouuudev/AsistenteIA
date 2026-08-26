using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConsultaEjecutadaRepository : IConsultaEjecutadaRepository
{
    private readonly AsistenteDbContext _context;

    public ConsultaEjecutadaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<ConsultaEjecutada?> GetByIdAsync(int id)
    {
        return await _context.ConsultasEjecutadas
            .Include(c => c.Usuario)
            .Include(c => c.Conexion)
            .FirstOrDefaultAsync(c => c.IdConsulta == id);
    }

    public async Task<IEnumerable<ConsultaEjecutada>> GetAllAsync()
    {
        return await _context.ConsultasEjecutadas
            .AsNoTracking()
            .Include(c => c.Usuario)
            .Include(c => c.Conexion)
            .OrderByDescending(c => c.FechaHora)
            .Take(200)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultaEjecutada>> GetByUsuarioIdAsync(int idUsuario)
    {
        return await _context.ConsultasEjecutadas
            .AsNoTracking()
            .Include(c => c.Usuario)
            .Include(c => c.Conexion)
            .Where(c => c.IdUsuario == idUsuario)
            .OrderByDescending(c => c.FechaHora)
            .Take(100)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultaEjecutada>> GetByConexionIdAsync(int idConexion)
    {
        return await _context.ConsultasEjecutadas
            .AsNoTracking()
            .Include(c => c.Usuario)
            .Include(c => c.Conexion)
            .Where(c => c.IdConexion == idConexion)
            .OrderByDescending(c => c.FechaHora)
            .Take(100)
            .ToListAsync();
    }

    public async Task AddAsync(ConsultaEjecutada consulta)
    {
        await _context.ConsultasEjecutadas.AddAsync(consulta);
    }
}
