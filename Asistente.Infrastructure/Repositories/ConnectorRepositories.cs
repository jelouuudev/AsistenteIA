using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ConnectorRepository : IConnectorRepository
{
    private readonly AsistenteDbContext _context;
    public ConnectorRepository(AsistenteDbContext context) => _context = context;

    public Task<Connector?> GetByIdAsync(int id, CancellationToken ct = default)
        => _context.Connectors
            .Include(c => c.Configuraciones)
            .Include(c => c.Credenciales)
            .Include(c => c.Politicas)
            .FirstOrDefaultAsync(c => c.IdConnector == id, ct);

    public Task<Connector?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        => _context.Connectors
            .Include(c => c.Configuraciones)
            .Include(c => c.Credenciales)
            .Include(c => c.Politicas)
            .FirstOrDefaultAsync(c => c.Codigo == codigo, ct);

    public Task<List<Connector>> GetAllAsync(CancellationToken ct = default)
        => _context.Connectors.OrderBy(c => c.Nombre).ToListAsync(ct);

    public async Task<Connector> AddAsync(Connector conector, CancellationToken ct = default)
    {
        await _context.Connectors.AddAsync(conector, ct);
        await _context.SaveChangesAsync(ct);
        return conector;
    }

    public async Task UpdateAsync(Connector conector, CancellationToken ct = default)
    {
        _context.Entry(conector).State = EntityState.Modified;
        await _context.SaveChangesAsync(ct);
    }
}

public class ConnectorExecutionRepository : IConnectorExecutionRepository
{
    private readonly AsistenteDbContext _context;
    public ConnectorExecutionRepository(AsistenteDbContext context) => _context = context;

    public async Task AddAsync(ConnectorExecution ejecucion, CancellationToken ct = default)
    {
        await _context.ConnectorEjecuciones.AddAsync(ejecucion, ct);
        await _context.SaveChangesAsync(ct);
    }

    public Task<List<ConnectorExecution>> GetByConnectorAsync(int idConnector, int tope = 100, CancellationToken ct = default)
        => _context.ConnectorEjecuciones
            .Where(e => e.IdConnector == idConnector)
            .OrderByDescending(e => e.Fecha)
            .Take(tope)
            .ToListAsync(ct);

    public async Task<ConnectorMetricas> GetMetricasAsync(int idConnector, CancellationToken ct = default)
    {
        var base_ = _context.ConnectorEjecuciones.Where(e => e.IdConnector == idConnector);
        var total = await base_.LongCountAsync(ct);
        if (total == 0)
            return new ConnectorMetricas { IdConnector = idConnector };
        return new ConnectorMetricas
        {
            IdConnector = idConnector,
            Total = total,
            Exitosas = await base_.LongCountAsync(e => e.Estado == "Exitoso", ct),
            Fallidas = await base_.LongCountAsync(e => e.Estado != "Exitoso", ct),
            LatenciaPromedioMs = await base_.AverageAsync(e => (double)e.LatenciaMs, ct),
            LatenciaMaxMs = await base_.MaxAsync(e => (double)e.LatenciaMs, ct),
            ReintentosTotales = await base_.SumAsync(e => (long)e.Reintentos, ct)
        };
    }
}
