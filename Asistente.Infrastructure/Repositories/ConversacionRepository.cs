using Asistente.Domain.Entities;
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
            .Include(c => c.Mensajes)
            .FirstOrDefaultAsync(c => c.IdConversacion == id);
    }

    public async Task UpdateAsync(Conversacion conversacion)
    {
        _context.Conversaciones.Update(conversacion);
    }
}
