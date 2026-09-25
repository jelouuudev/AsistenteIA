using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class EventoEmpresarialRepository : IEventoEmpresarialRepository
{
    private readonly AsistenteDbContext _context;
    public EventoEmpresarialRepository(AsistenteDbContext context) => _context = context;

    public async Task<EventoEmpresarial?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.EventosEmpresariales.AsNoTracking().FirstOrDefaultAsync(e => e.IdEvento == id, ct);

    public async Task<EventoEmpresarial?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        => await _context.EventosEmpresariales.AsNoTracking().FirstOrDefaultAsync(e => e.Codigo == codigo, ct);

    public async Task<IEnumerable<EventoEmpresarial>> GetAllAsync(CancellationToken ct = default)
        => await _context.EventosEmpresariales.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync(ct);

    public async Task<IEnumerable<EventoEmpresarial>> GetActivosAsync(CancellationToken ct = default)
        => await _context.EventosEmpresariales.AsNoTracking().Where(e => e.Activo).OrderBy(e => e.Nombre).ToListAsync(ct);

    public async Task AddAsync(EventoEmpresarial evento, CancellationToken ct = default)
        => await _context.EventosEmpresariales.AddAsync(evento, ct);

    public async Task UpdateAsync(EventoEmpresarial evento, CancellationToken ct = default)
    {
        _context.EventosEmpresariales.Update(evento);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(EventoEmpresarial evento, CancellationToken ct = default)
    {
        _context.EventosEmpresariales.Remove(evento);
        await Task.CompletedTask;
    }
}

public class DisparadorEventoRepository : IDisparadorEventoRepository
{
    private readonly AsistenteDbContext _context;
    public DisparadorEventoRepository(AsistenteDbContext context) => _context = context;

    public async Task<DisparadorEvento?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.DisparadoresEvento.AsNoTracking()
            .Include(d => d.Evento)
            .FirstOrDefaultAsync(d => d.IdDisparador == id, ct);

    public async Task<IEnumerable<DisparadorEvento>> GetAllAsync(CancellationToken ct = default)
        => await _context.DisparadoresEvento.AsNoTracking()
            .Include(d => d.Evento)
            .OrderBy(d => d.IdDisparador).ToListAsync(ct);

    public async Task<IEnumerable<DisparadorEvento>> GetActivosAsync(CancellationToken ct = default)
        => await _context.DisparadoresEvento.AsNoTracking()
            .Include(d => d.Evento)
            .Where(d => d.Activo)
            .OrderBy(d => d.IdDisparador).ToListAsync(ct);

    public async Task AddAsync(DisparadorEvento disparador, CancellationToken ct = default)
        => await _context.DisparadoresEvento.AddAsync(disparador, ct);

    public async Task UpdateAsync(DisparadorEvento disparador, CancellationToken ct = default)
    {
        _context.DisparadoresEvento.Update(disparador);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(DisparadorEvento disparador, CancellationToken ct = default)
    {
        _context.DisparadoresEvento.Remove(disparador);
        await Task.CompletedTask;
    }
}

public class ReglaEventoRepository : IReglaEventoRepository
{
    private readonly AsistenteDbContext _context;
    public ReglaEventoRepository(AsistenteDbContext context) => _context = context;

    public async Task<ReglaEvento?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.ReglasEvento.AsNoTracking().FirstOrDefaultAsync(r => r.IdRegla == id, ct);

    public async Task<IEnumerable<ReglaEvento>> GetAllAsync(CancellationToken ct = default)
        => await _context.ReglasEvento.AsNoTracking()
            .Include(r => r.Evento).Include(r => r.Workflow)
            .OrderByDescending(r => r.Prioridad).ToListAsync(ct);

    public async Task<IEnumerable<ReglaEvento>> GetByEventoAsync(int idEvento, CancellationToken ct = default)
        => await _context.ReglasEvento.AsNoTracking()
            .Include(r => r.Evento).Include(r => r.Workflow)
            .Where(r => r.IdEvento == idEvento)
            .OrderByDescending(r => r.Prioridad).ToListAsync(ct);

    public async Task<IEnumerable<ReglaEvento>> GetActivasAsync(CancellationToken ct = default)
        => await _context.ReglasEvento.AsNoTracking()
            .Include(r => r.Evento).Include(r => r.Workflow)
            .Where(r => r.Activa)
            .OrderByDescending(r => r.Prioridad).ToListAsync(ct);

    public async Task AddAsync(ReglaEvento regla, CancellationToken ct = default)
        => await _context.ReglasEvento.AddAsync(regla, ct);

    public async Task UpdateAsync(ReglaEvento regla, CancellationToken ct = default)
    {
        _context.ReglasEvento.Update(regla);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(ReglaEvento regla, CancellationToken ct = default)
    {
        _context.ReglasEvento.Remove(regla);
        await Task.CompletedTask;
    }

    public async Task DeleteByEventoAsync(int idEvento, CancellationToken ct = default)
    {
        await _context.ReglasEvento.Where(r => r.IdEvento == idEvento).ExecuteDeleteAsync(ct);
    }
}

public class EventoProcesadoRepository : IEventoProcesadoRepository
{
    private readonly AsistenteDbContext _context;
    public EventoProcesadoRepository(AsistenteDbContext context) => _context = context;

    public async Task<EventoProcesado?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.EventosProcesados.AsNoTracking().FirstOrDefaultAsync(e => e.IdEventoProcesado == id, ct);

    public async Task<IEnumerable<EventoProcesado>> GetAllAsync(CancellationToken ct = default)
        => await _context.EventosProcesados.AsNoTracking()
            .Include(e => e.Evento)
            .Include(e => e.Workflow)
            .OrderByDescending(e => e.FechaHora).ToListAsync(ct);

    public async Task<IEnumerable<EventoProcesado>> GetByEventoAsync(int idEvento, CancellationToken ct = default)
        => await _context.EventosProcesados.AsNoTracking()
            .Where(e => e.IdEvento == idEvento)
            .OrderByDescending(e => e.FechaHora).ToListAsync(ct);

    public async Task AddAsync(EventoProcesado evento, CancellationToken ct = default)
        => await _context.EventosProcesados.AddAsync(evento, ct);

    public async Task UpdateAsync(EventoProcesado evento, CancellationToken ct = default)
    {
        _context.EventosProcesados.Update(evento);
        await Task.CompletedTask;
    }
}

public class TareaProgramadaRepository : ITareaProgramadaRepository
{
    private readonly AsistenteDbContext _context;
    public TareaProgramadaRepository(AsistenteDbContext context) => _context = context;

    public async Task<TareaProgramada?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _context.TareasProgramadas.AsNoTracking().Include(t => t.Workflow).FirstOrDefaultAsync(t => t.IdTarea == id, ct);

    public async Task<IEnumerable<TareaProgramada>> GetAllAsync(CancellationToken ct = default)
        => await _context.TareasProgramadas.AsNoTracking().Include(t => t.Workflow).OrderBy(t => t.Nombre).ToListAsync(ct);

    public async Task<IEnumerable<TareaProgramada>> GetActivasAsync(CancellationToken ct = default)
        => await _context.TareasProgramadas.AsNoTracking().Include(t => t.Workflow).Where(t => t.Activa).OrderBy(t => t.Nombre).ToListAsync(ct);

    public async Task AddAsync(TareaProgramada tarea, CancellationToken ct = default)
        => await _context.TareasProgramadas.AddAsync(tarea, ct);

    public async Task UpdateAsync(TareaProgramada tarea, CancellationToken ct = default)
    {
        _context.TareasProgramadas.Update(tarea);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(TareaProgramada tarea, CancellationToken ct = default)
    {
        _context.TareasProgramadas.Remove(tarea);
        await Task.CompletedTask;
    }
}

public class ConfiguracionEventoMotorRepository : IConfiguracionEventoMotorRepository
{
    private readonly AsistenteDbContext _context;
    public ConfiguracionEventoMotorRepository(AsistenteDbContext context) => _context = context;

    public async Task<ConfiguracionEventoMotor?> GetAsync(CancellationToken ct = default)
        => await _context.ConfiguracionEventoMotor.FirstOrDefaultAsync(c => c.IdConfiguracion == 1, ct)
           ?? new ConfiguracionEventoMotor();

    public async Task UpdateAsync(ConfiguracionEventoMotor config, CancellationToken ct = default)
    {
        config.FechaActualizacion = System.DateTime.UtcNow;
        var existe = await _context.ConfiguracionEventoMotor.AnyAsync(c => c.IdConfiguracion == config.IdConfiguracion, ct);
        if (existe)
            _context.ConfiguracionEventoMotor.Update(config);
        else
            await _context.ConfiguracionEventoMotor.AddAsync(config, ct);
        await _context.SaveChangesAsync(ct);
    }
}
