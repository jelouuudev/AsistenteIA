using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IEjecucionHerramientaService
{
    Task<IEnumerable<EjecucionHerramientaDto>> ObtenerTodasAsync();
    Task<EjecucionHerramientaDto?> ObtenerPorIdAsync(int id);
}
