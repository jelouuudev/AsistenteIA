using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IConfiguracionMotorConsultasService
{
    Task<ConfiguracionMotorConsultasDto?> ObtenerActivaAsync(CancellationToken cancellationToken = default);
    Task<ConfiguracionMotorConsultasDto> ActualizarAsync(ActualizarConfiguracionMotorConsultasRequest request, CancellationToken cancellationToken = default);
}
