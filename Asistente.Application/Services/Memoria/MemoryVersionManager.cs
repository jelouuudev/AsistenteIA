using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Shared;

namespace Asistente.Application.Services.Memoria;

/// <summary>
/// Memory Version Manager (ETAPA 21): dueño del historial append-only de una memoria.
///
/// Aquí vive TODO lo que toca <see cref="MemoryVersion"/>: anexar la versión vigente,
/// listar el historial y recuperar el contenido de una versión concreta. El
/// <see cref="MemoryManager"/> no habla con el repositorio de versiones: compone
/// contenido y versión y se lo pasa a este componente, que lo persiste.
///
/// Reglas del append-only:
///  - Nunca se reescribe historia. Actualizar y revertir anexan una versión nueva.
///  - El número sale de <see cref="MemoryRecord.VersionActual"/>, que el Manager
///    incrementa antes de llamar. Así el contador vive en un solo sitio (la
///    memoria) y una versión nunca queda desalineada de su memoria.
///  - Este componente NO autoriza ni expira: el control de acceso es del Manager,
///    que es quien sabe quién es el usuario. Aquí solo se mueve historia.
///
/// Revertir, por tanto, no es una operación de este componente sino del Manager:
/// este entrega el contenido de la versión elegida y el Manager la escribe como
/// versión nueva por la vía normal de actualización.
/// </summary>
public class MemoryVersionManager
{
    private readonly IMemoryVersionRepository _versiones;

    public MemoryVersionManager(IMemoryVersionRepository versiones)
    {
        _versiones = versiones;
    }

    /// <summary>
    /// Anexa la versión que la memoria declara vigente (<c>VersionActual</c>).
    /// El motivo queda en la versión, no en la memoria: es la única traza de por
    /// qué el contenido cambió.
    /// </summary>
    public virtual async Task<MemoryVersion> RegistrarAsync(MemoryRecord memoria, string contenido,
        string? motivo, int? idUsuario, CancellationToken ct = default)
    {
        var version = new MemoryVersion
        {
            IdMemory = memoria.IdMemory,
            Numero = memoria.VersionActual,
            Contenido = contenido,
            Motivo = motivo,
            IdUsuario = idUsuario == 0 ? null : idUsuario,
            Fecha = DateTime.UtcNow
        };
        await _versiones.AddAsync(version, ct);
        return version;
    }

    /// <summary>Historial completo, de la versión más antigua a la más reciente.</summary>
    public virtual Task<List<MemoryVersion>> ListarAsync(int idMemory, CancellationToken ct = default)
        => _versiones.GetByMemoryAsync(idMemory, ct);

    /// <summary>Historial como DTO para la API y la interfaz.</summary>
    public virtual async Task<List<MemoryVersionDto>> ListarDtoAsync(int idMemory, CancellationToken ct = default)
        => (await ListarAsync(idMemory, ct))
            .Select(v => new MemoryVersionDto
            {
                Numero = v.Numero,
                Contenido = v.Contenido,
                Motivo = v.Motivo,
                Fecha = v.Fecha
            })
            .ToList();

    /// <summary>Contenido de una versión concreta; null si no existe.</summary>
    public virtual Task<MemoryVersion?> ObtenerAsync(int idMemory, int numero, CancellationToken ct = default)
        => _versiones.GetByNumeroAsync(idMemory, numero, ct);

    /// <summary>
    /// Número de versiones de una memoria. Lo usa el panel para el contador de
    /// historial sin tener que traer el contenido completo.
    /// </summary>
    public virtual async Task<int> ContarAsync(int idMemory, CancellationToken ct = default)
        => (await ListarAsync(idMemory, ct)).Count;
}