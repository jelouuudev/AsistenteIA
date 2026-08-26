using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AsistenteDbContext _context;

    public UsuarioRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<Usuario?> GetByIdAsync(int id)
    {
        return await _context.Usuarios
            .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);
    }

    public async Task<Usuario?> GetByUsernameAsync(string username)
    {
        return await _context.Usuarios
            .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.UsuarioNombre.ToLower() == username.ToLower());
    }

    public async Task<IEnumerable<Usuario>> GetAllAsync()
    {
        return await _context.Usuarios
            .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
            .ToListAsync();
    }

    public async Task AddAsync(Usuario usuario)
    {
        await _context.Usuarios.AddAsync(usuario);
    }

    public void Update(Usuario usuario)
    {
        _context.Usuarios.Update(usuario);
    }
}
