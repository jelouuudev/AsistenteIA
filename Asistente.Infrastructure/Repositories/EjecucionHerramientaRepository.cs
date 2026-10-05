using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class EjecucionHerramientaRepository : IEjecucionHerramientaRepository
{
    private readonly AsistenteDbContext _context;

    public EjecucionHerramientaRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EjecucionHerramienta ejecucion)
        => await _context.EjecucionesHerramientas.AddAsync(ejecucion);

    public async Task<IEnumerable<EjecucionHerramienta>> GetAllAsync(int top = 200)
        => await _context.EjecucionesHerramientas
            .AsNoTracking()
            .Include(e => e.Herramienta)
            .Include(e => e.Usuario)
            .OrderByDescending(e => e.FechaHora)
            .Take(top)
            .ToListAsync();

    public async Task<(int Total, double TiempoPromedio, int Errores, DateTime? Ultima)> GetEstadisticasAsync(int idHerramienta)
    {
        var ejecuciones = await _context.EjecucionesHerramientas
            .AsNoTracking()
            .Where(e => e.IdHerramienta == idHerramienta)
            .ToListAsync();

        if (ejecuciones.Count == 0)
            return (0, 0, 0, null);

        var errores = ejecuciones.Count(e => e.Estado == "Error" || e.Estado == "Rechazada");
        var promedio = ejecuciones.Average(e => (double)e.TiempoEjecucion);
        var ultima = ejecuciones.Max(e => e.FechaHora);

        return (ejecuciones.Count, promedio, errores, ultima);
    }

    public async Task<(long Total, long ConsultasSql)> ContarAsync(CancellationToken ct = default)
    {
        var total = await _context.EjecucionesHerramientas.LongCountAsync(ct);
        var sql = await _context.EjecucionesHerramientas
            .Join(_context.Herramientas,
                e => e.IdHerramienta,
                h => h.IdHerramienta,
                (e, h) => h.Codigo)
            .LongCountAsync(codigo => codigo == "SqlQueryTool", ct);
        return (total, sql);
    }

    public async Task<IEnumerable<EjecucionHerramienta>> GetByHerramientaAsync(int idHerramienta, int top = 50)
        => await _context.EjecucionesHerramientas
            .AsNoTracking()
            .Include(e => e.Herramienta)
            .Include(e => e.Usuario)
            .Where(e => e.IdHerramienta == idHerramienta)
            .OrderByDescending(e => e.FechaHora)
            .Take(top)
            .ToListAsync();
}
