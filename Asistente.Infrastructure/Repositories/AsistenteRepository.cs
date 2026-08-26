using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class AsistenteRepository : IAsistenteRepository
{
    private readonly AsistenteDbContext _context;

    public AsistenteRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.Entities.Asistente?> GetByIdAsync(int id)
    {
        return await _context.Asistentes
            .Include(a => a.PromptsSistema)
            .Include(a => a.AsistentesFuentes)
            .Include(a => a.AsistentesHerramientas)
            .Include(a => a.AgentesWorkflows)
            .Include(a => a.AgentesRoles)
            .Include(a => a.Versiones)
            .Include(a => a.UsuariosAsistentes)
            .FirstOrDefaultAsync(a => a.IdAsistente == id);
    }

    public async Task<Domain.Entities.Asistente?> GetByCodigoAsync(string codigo)
    {
        return await _context.Asistentes
            .FirstOrDefaultAsync(a => a.Codigo == codigo);
    }

    public async Task<IEnumerable<Domain.Entities.Asistente>> GetAllAsync()
    {
        return await _context.Asistentes
            .Include(a => a.AsistentesHerramientas)
                .ThenInclude(h => h.Herramienta)
            .Include(a => a.PromptsSistema)
            .ToListAsync();
    }

    /// <summary>
    /// Agentes que el usuario puede utilizar (Regla 1): asignados por rol o directamente.
    /// </summary>
    public async Task<IEnumerable<Domain.Entities.Asistente>> GetAutorizadosParaUsuarioAsync(
        int idUsuario, IEnumerable<int> rolesUsuario)
    {
        var roles = rolesUsuario?.ToList() ?? new List<int>();
        var esAdmin = roles.Contains(1); // Rol 1 = Administrador: ve todos los agentes activos (cualquier estado de ciclo de vida).

        var query = _context.Asistentes.Where(a => a.Activo);

        if (esAdmin)
        {
            // El administrador ve TODOS los agentes activos (sin restricción de asignación ni de estado de ciclo de vida).
            return await query.ToListAsync();
        }

        // Usuarios no administradores: agentes activos a los que estén autorizados
        // (por rol asignado o asignación directa), sin importar el estado de ciclo de vida.
        // Así pueden usar/probar el agente que se les asignó (Regla 1).
        query = query.Where(a => a.UsuariosAsistentes.Any(ua => ua.IdUsuario == idUsuario && ua.Activo)
                                 || a.AgentesRoles.Any(ar => ar.Activo && roles.Contains(ar.IdRol)));

        return await query.ToListAsync();
    }

    public async Task<List<string>> GetHerramientasActivasAsync(int idAgente)
    {
        return await _context.AsistentesHerramientas
            .AsNoTracking()
            .Where(ah => ah.IdAsistente == idAgente && ah.Activa)
            .Include(ah => ah.Herramienta)
            .Select(ah => ah.Herramienta!.Codigo)
            .ToListAsync();
    }

    public async Task AddAsync(Domain.Entities.Asistente asistente)
    {
        await _context.Asistentes.AddAsync(asistente);
    }

    public void Update(Domain.Entities.Asistente asistente)
    {
        _context.Asistentes.Update(asistente);
    }

    public void Delete(Domain.Entities.Asistente asistente)
    {
        _context.Asistentes.Remove(asistente);
    }

    /// <summary>
    /// Elimina (de la BD y del change tracker) todas las asignaciones del agente
    /// (fuentes, herramientas, workflows, roles y usuarios) para poder reinsertarlas
    /// sin conflicto de tracking al editar.
    /// </summary>
    public async Task EliminarAsignacionesAsync(int idAsistente)
    {
        var fuentes = _context.AsistentesFuentes.Where(x => x.IdAsistente == idAsistente);
        _context.AsistentesFuentes.RemoveRange(fuentes);
        var herramientas = _context.AsistentesHerramientas.Where(x => x.IdAsistente == idAsistente);
        _context.AsistentesHerramientas.RemoveRange(herramientas);
        var workflows = _context.AgentesWorkflows.Where(x => x.IdAsistente == idAsistente);
        _context.AgentesWorkflows.RemoveRange(workflows);
        var roles = _context.AgentesRoles.Where(x => x.IdAsistente == idAsistente);
        _context.AgentesRoles.RemoveRange(roles);
        var usuarios = _context.UsuarioAsistentes.Where(x => x.IdAsistente == idAsistente);
        _context.UsuarioAsistentes.RemoveRange(usuarios);
        await _context.SaveChangesAsync();
    }
}
