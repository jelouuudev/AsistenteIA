using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IQueryEmpresarialService
{
    Task<ResultadoProcesarPreguntaDto> ProcesarPreguntaAsync(
        string pregunta,
        int idUsuario,
        CancellationToken cancellationToken = default);

    Task<EjecutarConsultaResponse> EjecutarConsultaAsync(
        EjecutarConsultaRequest request,
        int idUsuario,
        CancellationToken cancellationToken = default);
}
