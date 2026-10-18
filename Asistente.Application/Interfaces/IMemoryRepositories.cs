using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Application.Interfaces;

/// <summary>Repositorios de Memoria Empresarial (ETAPA 21).</summary>
public interface IMemoryRepository
{
    Task<MemoryRecord?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MemoryRecord?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
    Task<List<MemoryRecord>> GetActivasAsync(CancellationToken ct = default);
    Task<List<MemoryRecord>> GetByScopeAsync(int idScope, CancellationToken ct = default);
    Task<MemoryRecord> AddAsync(MemoryRecord memoria, CancellationToken ct = default);
    Task UpdateAsync(MemoryRecord memoria, CancellationToken ct = default);
    Task DeleteAsync(MemoryRecord memoria, CancellationToken ct = default);
    Task<List<MemoryScope>> GetScopesAsync(CancellationToken ct = default);
    Task<List<MemoryCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<List<MemoryRetentionPolicy>> GetPoliticasAsync(CancellationToken ct = default);
}

public interface IMemoryVersionRepository
{
    Task AddAsync(MemoryVersion version, CancellationToken ct = default);
    Task<List<MemoryVersion>> GetByMemoryAsync(int idMemory, CancellationToken ct = default);
    Task<MemoryVersion?> GetByNumeroAsync(int idMemory, int numero, CancellationToken ct = default);
}

public interface IMemoryAccessLogRepository
{
    Task AddAsync(MemoryAccessLog log, CancellationToken ct = default);
    Task<List<MemoryAccessLog>> GetByMemoryAsync(int idMemory, int tope = 100, CancellationToken ct = default);
    Task<long> ContarPorOperacionAsync(string operacion, CancellationToken ct = default);

    /// <summary>Observabilidad: número de accesos por agente (Id null = sin agente).</summary>
    Task<List<(int? Agente, long Total)>> ContarPorAgenteAsync(CancellationToken ct = default);

    /// <summary>Observabilidad: número de accesos por usuario (Id null = sin usuario).</summary>
    Task<List<(int? Usuario, long Total)>> ContarPorUsuarioAsync(CancellationToken ct = default);

    /// <summary>Observabilidad: duración media de las operaciones que la registran.</summary>
    Task<double?> DuracionMediaMsAsync(string operacion, CancellationToken ct = default);

    /// <summary>Observabilidad: accesos agrupados por tipo de memoria (ámbito).</summary>
    Task<List<(string? Ambito, long Total)>> ContarPorTipoAsync(CancellationToken ct = default);
}
