using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Seguridad;

/// <summary>
/// Servicio de permisos granulares por módulo (ETAPA 14 - Actividad 3).
/// </summary>
public class PermisoService : IPermisoService
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PermisoService(IPermisoRepository permisoRepository, IUnitOfWork unitOfWork)
    {
        _permisoRepository = permisoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<PermisoDto>> ObtenerTodosAsync(CancellationToken ct = default)
        => (await _permisoRepository.GetAllAsync(ct)).Select(Map);

    public async Task<IEnumerable<string>> ObtenerCodigosPorUsuarioAsync(int idUsuario, CancellationToken ct = default)
        => await _permisoRepository.ObtenerCodigosPorUsuarioAsync(idUsuario, ct);

    public async Task<bool> TienePermisoAsync(int idUsuario, string codigoPermiso, CancellationToken ct = default)
    {
        var codigos = await _permisoRepository.ObtenerCodigosPorUsuarioAsync(idUsuario, ct);
        return codigos.Contains(codigoPermiso);
    }

    public async Task AsignarPermisosRolAsync(int idRol, IEnumerable<string> codigos, CancellationToken ct = default)
    {
        foreach (var codigo in codigos)
        {
            var permiso = await _permisoRepository.GetByCodigoAsync(codigo, ct);
            if (permiso != null)
                await _permisoRepository.AddRolPermisoAsync(new RolPermiso { IdRol = idRol, IdPermiso = permiso.IdPermiso }, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<PermisoDto>> ObtenerPorRolAsync(int idRol, CancellationToken ct = default)
        => (await _permisoRepository.GetByRolAsync(idRol, ct)).Select(Map);

    public async Task ReemplazarPermisosRolAsync(int idRol, IEnumerable<string> codigos, CancellationToken ct = default)
    {
        await _permisoRepository.DeleteByRolAsync(idRol, ct);
        foreach (var codigo in codigos.Distinct())
        {
            var permiso = await _permisoRepository.GetByCodigoAsync(codigo, ct);
            if (permiso != null)
                await _permisoRepository.AddRolPermisoAsync(new RolPermiso { IdRol = idRol, IdPermiso = permiso.IdPermiso }, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task CrearPermisoAsync(CrearPermisoRequest request, CancellationToken ct = default)
    {
        await _permisoRepository.AddAsync(new Permiso
        {
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Modulo = request.Modulo,
            Activo = true
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static PermisoDto Map(Permiso p) => new()
    {
        IdPermiso = p.IdPermiso,
        Codigo = p.Codigo,
        Nombre = p.Nombre,
        Descripcion = p.Descripcion,
        Modulo = p.Modulo,
        Activo = p.Activo
    };
}
