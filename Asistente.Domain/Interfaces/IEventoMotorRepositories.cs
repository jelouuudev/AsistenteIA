using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IEventoEmpresarialRepository
{
    Task<EventoEmpresarial?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<EventoEmpresarial?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
    Task<IEnumerable<EventoEmpresarial>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<EventoEmpresarial>> GetActivosAsync(CancellationToken ct = default);
    Task AddAsync(EventoEmpresarial evento, CancellationToken ct = default);
    Task UpdateAsync(EventoEmpresarial evento, CancellationToken ct = default);
    Task DeleteAsync(EventoEmpresarial evento, CancellationToken ct = default);
}

public interface IReglaEventoRepository
{
    Task<ReglaEvento?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<ReglaEvento>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<ReglaEvento>> GetByEventoAsync(int idEvento, CancellationToken ct = default);
    Task<IEnumerable<ReglaEvento>> GetActivasAsync(CancellationToken ct = default);
    Task AddAsync(ReglaEvento regla, CancellationToken ct = default);
    Task UpdateAsync(ReglaEvento regla, CancellationToken ct = default);
    Task DeleteAsync(ReglaEvento regla, CancellationToken ct = default);
    Task DeleteByEventoAsync(int idEvento, CancellationToken ct = default);
}

public interface IEventoProcesadoRepository
{
    Task<EventoProcesado?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<EventoProcesado>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<EventoProcesado>> GetByEventoAsync(int idEvento, CancellationToken ct = default);
    Task AddAsync(EventoProcesado evento, CancellationToken ct = default);
    Task UpdateAsync(EventoProcesado evento, CancellationToken ct = default);
}

public interface ITareaProgramadaRepository
{
    Task<TareaProgramada?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<TareaProgramada>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<TareaProgramada>> GetActivasAsync(CancellationToken ct = default);
    Task AddAsync(TareaProgramada tarea, CancellationToken ct = default);
    Task UpdateAsync(TareaProgramada tarea, CancellationToken ct = default);
    Task DeleteAsync(TareaProgramada tarea, CancellationToken ct = default);
}

public interface IConfiguracionEventoMotorRepository
{
    Task<ConfiguracionEventoMotor?> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(ConfiguracionEventoMotor config, CancellationToken ct = default);
}
