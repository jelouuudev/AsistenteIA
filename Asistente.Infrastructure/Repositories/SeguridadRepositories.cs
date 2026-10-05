using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class PermisoRepository : IPermisoRepository
{
    private readonly AsistenteDbContext _context;
    public PermisoRepository(AsistenteDbContext context) => _context = context;

    public async Task<Permiso?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        => await _context.Permisos.AsNoTracking().FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

    public async Task<IEnumerable<Permiso>> GetAllAsync(CancellationToken ct = default)
        => await _context.Permisos.AsNoTracking().OrderBy(p => p.Modulo).ThenBy(p => p.Codigo).ToListAsync(ct);

    public async Task<IEnumerable<Permiso>> GetByRolAsync(int idRol, CancellationToken ct = default)
        => await _context.RolPermisos.AsNoTracking()
            .Where(rp => rp.IdRol == idRol)
            .Include(rp => rp.Permiso)
            .Select(rp => rp.Permiso!)
            .ToListAsync(ct);

    public async Task AddAsync(Permiso permiso, CancellationToken ct = default)
        => await _context.Permisos.AddAsync(permiso, ct);

    public async Task AddRolPermisoAsync(RolPermiso rp, CancellationToken ct = default)
        => await _context.RolPermisos.AddAsync(rp, ct);

    public async Task DeleteByRolAsync(int idRol, CancellationToken ct = default)
    {
        var existentes = await _context.RolPermisos.Where(rp => rp.IdRol == idRol).ToListAsync(ct);
        _context.RolPermisos.RemoveRange(existentes);
    }

    public async Task<IEnumerable<string>> ObtenerCodigosPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        return await _context.UsuarioRoles.AsNoTracking()
            .Where(ur => ur.IdUsuario == idUsuario)
            .SelectMany(ur => _context.RolPermisos.Where(rp => rp.IdRol == ur.IdRol).Select(rp => rp.IdPermiso))
            .Join(_context.Permisos, idp => idp, p => p.IdPermiso, (_, p) => p.Codigo)
            .Distinct()
            .ToListAsync(ct);
    }
}

public class UsuarioAsistenteRepository : IUsuarioAsistenteRepository
{
    private readonly AsistenteDbContext _context;
    public UsuarioAsistenteRepository(AsistenteDbContext context) => _context = context;

    public async Task<IEnumerable<int>> GetAsistentesAutorizadosAsync(int idUsuario, CancellationToken ct = default)
        => await _context.UsuarioAsistentes.AsNoTracking()
            .Where(ua => ua.IdUsuario == idUsuario && ua.Activo)
            .Select(ua => ua.IdAsistente).ToListAsync(ct);

    public async Task<bool> EstaAutorizadoAsync(int idUsuario, int idAsistente, CancellationToken ct = default)
        => await _context.UsuarioAsistentes.AsNoTracking()
            .AnyAsync(ua => ua.IdUsuario == idUsuario && ua.IdAsistente == idAsistente && ua.Activo, ct);

    public async Task AddAsync(UsuarioAsistente ua, CancellationToken ct = default)
        => await _context.UsuarioAsistentes.AddAsync(ua, ct);

    public async Task DeleteByUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var existentes = await _context.UsuarioAsistentes.Where(x => x.IdUsuario == idUsuario).ToListAsync(ct);
        _context.UsuarioAsistentes.RemoveRange(existentes);
    }
}

public class UsuarioFuenteRepository : IUsuarioFuenteRepository
{
    private readonly AsistenteDbContext _context;
    public UsuarioFuenteRepository(AsistenteDbContext context) => _context = context;

    public async Task<IEnumerable<int>> GetFuentesAutorizadasAsync(int idUsuario, CancellationToken ct = default)
        => await _context.UsuarioFuentes.AsNoTracking()
            .Where(uf => uf.IdUsuario == idUsuario && uf.Activo)
            .Select(uf => uf.IdFuente).ToListAsync(ct);

    public async Task<bool> EstaAutorizadaAsync(int idUsuario, int idFuente, CancellationToken ct = default)
        => await _context.UsuarioFuentes.AsNoTracking()
            .AnyAsync(uf => uf.IdUsuario == idUsuario && uf.IdFuente == idFuente && uf.Activo, ct);

    public async Task AddAsync(UsuarioFuente uf, CancellationToken ct = default)
        => await _context.UsuarioFuentes.AddAsync(uf, ct);

    public async Task DeleteByUsuarioAsync(int idUsuario, CancellationToken ct = default)
    {
        var existentes = await _context.UsuarioFuentes.Where(x => x.IdUsuario == idUsuario).ToListAsync(ct);
        _context.UsuarioFuentes.RemoveRange(existentes);
    }
}

public class PoliticaIARepository : IPoliticaIARepository
{
    private readonly AsistenteDbContext _context;
    public PoliticaIARepository(AsistenteDbContext context) => _context = context;

    public async Task<IEnumerable<PoliticaIA>> GetActivasAsync(CancellationToken ct = default)
        => await _context.PoliticasIA.AsNoTracking().Where(p => p.Activa).ToListAsync(ct);

    public async Task<PoliticaIA?> GetByTipoAsync(string tipo, CancellationToken ct = default)
        => await _context.PoliticasIA.AsNoTracking().FirstOrDefaultAsync(p => p.Tipo == tipo && p.Activa, ct);

    public async Task AddAsync(PoliticaIA p, CancellationToken ct = default)
        => await _context.PoliticasIA.AddAsync(p, ct);

    public async Task UpdateAsync(PoliticaIA p, CancellationToken ct = default)
        => _context.PoliticasIA.Update(p);
}

public class AuditoriaActividadRepository : IAuditoriaActividadRepository
{
    private readonly AsistenteDbContext _context;
    public AuditoriaActividadRepository(AsistenteDbContext context) => _context = context;

    public async Task AddAsync(AuditoriaActividad a, CancellationToken ct = default)
        => await _context.AuditoriaActividad.AddAsync(a, ct);

    public async Task<IEnumerable<AuditoriaActividad>> GetAllAsync(int pagina, int tamano, CancellationToken ct = default)
        => await _context.AuditoriaActividad.AsNoTracking()
            .Include(a => a.Usuario)
            .OrderByDescending(a => a.FechaHora)
            .Skip(pagina * tamano).Take(tamano).ToListAsync(ct);

    public async Task<long> CountAsync(CancellationToken ct = default)
        => await _context.AuditoriaActividad.LongCountAsync(ct);

    public async Task<long> CountByFiltroAsync(string? modulo = null, string? accion = null, string? resultado = null, CancellationToken ct = default)
    {
        var q = _context.AuditoriaActividad.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(modulo)) q = q.Where(a => a.Modulo == modulo);
        if (!string.IsNullOrEmpty(accion)) q = q.Where(a => a.Accion == accion);
        if (!string.IsNullOrEmpty(resultado)) q = q.Where(a => a.Resultado == resultado);
        return await q.LongCountAsync(ct);
    }
}

public class AuditoriaIARepository : IAuditoriaIARepository
{
    private readonly AsistenteDbContext _context;
    public AuditoriaIARepository(AsistenteDbContext context) => _context = context;

    public async Task AddAsync(AuditoriaIA a, CancellationToken ct = default)
        => await _context.AuditoriaIA.AddAsync(a, ct);

    public async Task<IEnumerable<AuditoriaIA>> GetByUsuarioAsync(int idUsuario, CancellationToken ct = default)
        => await _context.AuditoriaIA.AsNoTracking()
            .Where(a => a.IdUsuario == idUsuario).OrderByDescending(a => a.FechaHora).Take(100).ToListAsync(ct);

    public async Task<IEnumerable<AuditoriaIA>> GetRecientesAsync(int cantidad, CancellationToken ct = default)
        => await _context.AuditoriaIA.AsNoTracking().OrderByDescending(a => a.FechaHora).Take(cantidad).ToListAsync(ct);
}

public class MetricasIARepository : IMetricasIARepository
{
    private readonly AsistenteDbContext _context;
    public MetricasIARepository(AsistenteDbContext context) => _context = context;

    public async Task AddAsync(MetricasIA m, CancellationToken ct = default)
        => await _context.MetricasIA.AddAsync(m, ct);

    public async Task<IEnumerable<MetricasIA>> GetRecientesAsync(int cantidad, CancellationToken ct = default)
        => await _context.MetricasIA.AsNoTracking().OrderByDescending(m => m.FechaHora).Take(cantidad).ToListAsync(ct);
}
