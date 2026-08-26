using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IPermisoRepository
{
    Task<Permiso?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
    Task<IEnumerable<Permiso>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Permiso>> GetByRolAsync(int idRol, CancellationToken ct = default);
    Task AddAsync(Permiso permiso, CancellationToken ct = default);
    Task AddRolPermisoAsync(RolPermiso rp, CancellationToken ct = default);
    Task<IEnumerable<string>> ObtenerCodigosPorUsuarioAsync(int idUsuario, CancellationToken ct = default);
}

public interface IUsuarioAsistenteRepository
{
    Task<IEnumerable<int>> GetAsistentesAutorizadosAsync(int idUsuario, CancellationToken ct = default);
    Task<bool> EstaAutorizadoAsync(int idUsuario, int idAsistente, CancellationToken ct = default);
    Task AddAsync(UsuarioAsistente ua, CancellationToken ct = default);
    Task DeleteByUsuarioAsync(int idUsuario, CancellationToken ct = default);
}

public interface IUsuarioFuenteRepository
{
    Task<IEnumerable<int>> GetFuentesAutorizadasAsync(int idUsuario, CancellationToken ct = default);
    Task<bool> EstaAutorizadaAsync(int idUsuario, int idFuente, CancellationToken ct = default);
    Task AddAsync(UsuarioFuente uf, CancellationToken ct = default);
    Task DeleteByUsuarioAsync(int idUsuario, CancellationToken ct = default);
}

public interface IPoliticaIARepository
{
    Task<IEnumerable<PoliticaIA>> GetActivasAsync(CancellationToken ct = default);
    Task<PoliticaIA?> GetByTipoAsync(string tipo, CancellationToken ct = default);
    Task AddAsync(PoliticaIA p, CancellationToken ct = default);
    Task UpdateAsync(PoliticaIA p, CancellationToken ct = default);
}

public interface IAuditoriaActividadRepository
{
    Task AddAsync(AuditoriaActividad a, CancellationToken ct = default);
    Task<IEnumerable<AuditoriaActividad>> GetAllAsync(int pagina, int tamano, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
    Task<long> CountByFiltroAsync(string? modulo = null, string? accion = null, string? resultado = null, CancellationToken ct = default);
}

public interface IAuditoriaIARepository
{
    Task AddAsync(AuditoriaIA a, CancellationToken ct = default);
    Task<IEnumerable<AuditoriaIA>> GetByUsuarioAsync(int idUsuario, CancellationToken ct = default);
    Task<IEnumerable<AuditoriaIA>> GetRecientesAsync(int cantidad, CancellationToken ct = default);
}

public interface IMetricasIARepository
{
    Task AddAsync(MetricasIA m, CancellationToken ct = default);
    Task<IEnumerable<MetricasIA>> GetRecientesAsync(int cantidad, CancellationToken ct = default);
}
