using System.Threading;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

/// <summary>
/// Panel de memoria empresarial (ETAPA 21, secciones 7 y 17).
///
/// Un solo golpe de vista con lo que un operador necesita para saber si la memoria
/// está funcionando: qué hay guardado, quién la está reutilizando, a cuánto tarda
/// y qué tiene la retención pendiente de apagar.
/// </summary>
public interface IDashboardMemoriaService
{
    Task<DashboardMemoriaDto> ObtenerAsync(CancellationToken ct = default);
}
