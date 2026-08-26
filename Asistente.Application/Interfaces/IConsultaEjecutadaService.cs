using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IConsultaEjecutadaService
{
    Task<IEnumerable<ConsultaEjecutadaDto>> ObtenerTodasAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ConsultaEjecutadaDto>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);
    Task<IEnumerable<ConsultaEjecutadaDto>> ObtenerPorConexionAsync(int idConexion, CancellationToken cancellationToken = default);
    Task<ConsultaEjecutadaDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DashboardConsultasDto> ObtenerDashboardAsync(CancellationToken cancellationToken = default);
}
