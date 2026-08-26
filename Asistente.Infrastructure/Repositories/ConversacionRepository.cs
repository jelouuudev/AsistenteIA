using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConversacionRepository : IConversacionRepository
{
    private readonly AsistenteDbContext _context;

    public ConversacionRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<Conversacion> CreateAsync(Conversacion conversacion)
    {
        _context.Conversaciones.Add(conversacion);
        return conversacion;
    }

    public async Task<Conversacion?> GetByIdAsync(int id)
    {
        return await _context.Conversaciones
            .Include(c => c.Mensajes.OrderBy(m => m.FechaHora))
            .FirstOrDefaultAsync(c => c.IdConversacion == id);
    }

    public async Task UpdateAsync(Conversacion conversacion)
    {
        _context.Conversaciones.Update(conversacion);
    }

    public async Task<IEnumerable<Conversacion>> GetByUsuarioAsync(int usuarioId)
    {
        return await _context.Conversaciones
            .Where(c => c.UsuarioPropietario == usuarioId && c.Estado != EstadoConversacion.Eliminada)
            .OrderByDescending(c => c.FechaUltimaActividad ?? c.FechaInicio)
            .ToListAsync();
    }

    public async Task<IEnumerable<Conversacion>> GetAllAsync()
    {
        return await _context.Conversaciones
            .Where(c => c.Estado != EstadoConversacion.Eliminada)
            .OrderByDescending(c => c.FechaUltimaActividad ?? c.FechaInicio)
            .ToListAsync();
    }

    public async Task<IEnumerable<Conversacion>> SearchByTituloAsync(int usuarioId, string titulo)
    {
        return await _context.Conversaciones
            .Where(c => c.UsuarioPropietario == usuarioId
                && c.Estado != EstadoConversacion.Eliminada
                && c.Titulo != null
                && c.Titulo.Contains(titulo))
            .OrderByDescending(c => c.FechaUltimaActividad ?? c.FechaInicio)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Conversaciones.AnyAsync(c => c.IdConversacion == id);
    }
}
