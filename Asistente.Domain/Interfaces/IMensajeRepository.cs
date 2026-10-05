using System.Threading;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Interfaces;

public interface IMensajeRepository
{
    Task<Mensaje> CreateAsync(Mensaje mensaje);
    Task<IEnumerable<Mensaje>> GetByConversacionIdAsync(int conversacionId);
    Task<int> CountByConversacionIdAsync(int conversacionId);
    Task<long> CountByRolAsync(RolMensaje rol, CancellationToken ct = default);
}
