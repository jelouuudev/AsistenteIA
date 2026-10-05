using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IHerramientaRepository
{
    Task<Herramienta?> GetByIdAsync(int id);
    Task<Herramienta?> GetByCodigoAsync(string codigo);
    Task<IEnumerable<Herramienta>> GetAllAsync();
    Task<IEnumerable<Herramienta>> GetActivasAsync();
    Task AddAsync(Herramienta herramienta);
    Task UpdateAsync(Herramienta herramienta);
    Task DeleteByIdAsync(int id);
}

public interface IAsistenteHerramientaRepository
{
    Task<IEnumerable<Herramienta>> GetHerramientasPorAsistenteAsync(int idAsistente);
    Task<IEnumerable<AsistenteHerramienta>> ObtenerPorHerramientaAsync(int idHerramienta);
    Task<AsistenteHerramienta?> GetAsync(int idAsistente, int idHerramienta);
    Task AddAsync(AsistenteHerramienta relacion);
    Task UpdateAsync(AsistenteHerramienta relacion);
    Task DeleteAsync(AsistenteHerramienta relacion);
}

public interface IEjecucionHerramientaRepository
{
    Task AddAsync(EjecucionHerramienta ejecucion);
    Task<IEnumerable<EjecucionHerramienta>> GetAllAsync(int top = 200);
    Task<(int Total, double TiempoPromedio, int Errores, DateTime? Ultima)> GetEstadisticasAsync(int idHerramienta);
    Task<IEnumerable<EjecucionHerramienta>> GetByHerramientaAsync(int idHerramienta, int top = 50);
    Task<(long Total, long ConsultasSql)> ContarAsync(CancellationToken ct = default);
}

public interface IConfiguracionOrchestratorRepository
{
    Task<ConfiguracionOrchestrator?> GetAsync();
    Task UpdateAsync(ConfiguracionOrchestrator config);
}
