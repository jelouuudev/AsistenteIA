using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class RolRepository : IRolRepository
{
    private readonly AsistenteDbContext _context;

    public RolRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<Rol?> GetByIdAsync(int id)
    {
        return await _context.Roles.FindAsync(id);
    }

    public async Task<Rol?> GetByNombreAsync(string nombre)
    {
        return await _context.Roles
            .FirstOrDefaultAsync(r => r.Nombre.ToLower() == nombre.ToLower());
    }

    public async Task<IEnumerable<Rol>> GetAllAsync()
    {
        return await _context.Roles.ToListAsync();
    }

    public async Task AddAsync(Rol rol)
    {
        await _context.Roles.AddAsync(rol);
    }

    public void Update(Rol rol)
    {
        _context.Roles.Update(rol);
    }
}
