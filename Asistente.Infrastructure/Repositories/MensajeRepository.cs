using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class MensajeRepository : IMensajeRepository
{
    private readonly AsistenteDbContext _context;

    public MensajeRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<Mensaje> CreateAsync(Mensaje mensaje)
    {
        _context.Mensajes.Add(mensaje);
        return mensaje;
    }

    public async Task<IEnumerable<Mensaje>> GetByConversacionIdAsync(int conversacionId)
    {
        return await _context.Mensajes
            .Where(m => m.IdConversacion == conversacionId)
            .OrderBy(m => m.FechaHora)
            .ToListAsync();
    }
}
