using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Application.Interfaces;

/// <summary>Repositorio de conectores externos (ETAPA 20).</summary>
public interface IConnectorRepository
{
    Task<Connector?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Connector?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
    Task<List<Connector>> GetAllAsync(CancellationToken ct = default);
    Task<Connector> AddAsync(Connector conector, CancellationToken ct = default);
    Task UpdateAsync(Connector conector, CancellationToken ct = default);
}

/// <summary>Repositorio de ejecuciones (auditoría) y métricas de conectores.</summary>
public interface IConnectorExecutionRepository
{
    Task AddAsync(ConnectorExecution ejecucion, CancellationToken ct = default);
    Task<List<ConnectorExecution>> GetByConnectorAsync(int idConnector, int tope = 100, CancellationToken ct = default);
    Task<ConnectorMetricas> GetMetricasAsync(int idConnector, CancellationToken ct = default);
}

/// <summary>Métricas agregadas por conector para el Dashboard.</summary>
public class ConnectorMetricas
{
    public int IdConnector { get; set; }
    public long Total { get; set; }
    public long Exitosas { get; set; }
    public long Fallidas { get; set; }
    public double LatenciaPromedioMs { get; set; }
    public double LatenciaMaxMs { get; set; }
    public long ReintentosTotales { get; set; }
}
