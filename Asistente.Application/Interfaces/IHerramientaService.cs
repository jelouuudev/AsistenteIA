using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Application.Interfaces;

public interface IHerramientaService
{
    Task<IEnumerable<HerramientaDto>> ObtenerTodasAsync(int? idAsistente = null);
    Task<HerramientaDto?> ObtenerPorIdAsync(int id);
    Task<HerramientaDto> CrearAsync(CrearHerramientaRequest request);
    Task ActualizarAsync(int id, ActualizarHerramientaRequest request);
    Task EliminarAsync(int id);
    Task ActivarAsync(int id);
    Task DesactivarAsync(int id);
    Task<IEnumerable<HerramientaDto>> ObtenerAsociadasAlAsistenteAsync(int idAsistente);
    Task AsociarHerramientaAsync(int idAsistente, int idHerramienta, bool activa);
    Task DesasociarHerramientaAsync(int idAsistente, int idHerramienta);
    Task<ConfiguracionOrchestratorDto> ObtenerConfiguracionAsync();
    Task GuardarConfiguracionAsync(ConfiguracionOrchestratorDto config);
}
