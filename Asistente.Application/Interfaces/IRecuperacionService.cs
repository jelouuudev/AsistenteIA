using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IRecuperacionService
{
    Task<string> RecuperarContextoAsync(string pregunta, CancellationToken cancellationToken = default);
    Task<(string Contexto, List<ReferenciaDocumentalDto> Referencias)> RecuperarContextoConFuentesAsync(string pregunta, int? idAsistente = null, CancellationToken cancellationToken = default);
}
