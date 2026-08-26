using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IConsultaPlantillaService
{
    Task<IEnumerable<ConsultaPlantillaDto>> ObtenerTodasAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ConsultaPlantillaDto>> ObtenerActivasAsync(CancellationToken cancellationToken = default);
    Task<ConsultaPlantillaDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ConsultaPlantillaDto> CrearAsync(CrearConsultaPlantillaRequest request, CancellationToken cancellationToken = default);
    Task<ConsultaPlantillaDto> ActualizarAsync(int id, ActualizarConsultaPlantillaRequest request, CancellationToken cancellationToken = default);
    Task ActivarAsync(int id, CancellationToken cancellationToken = default);
    Task DesactivarAsync(int id, CancellationToken cancellationToken = default);
    Task EliminarAsync(int id, CancellationToken cancellationToken = default);
}
