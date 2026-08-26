using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Asistente.Domain.Interfaces;

public interface ISqlQueryExecutor
{
    Task<IEnumerable<Dictionary<string, object?>>> ExecuteReadOnlyAsync(
        string connectionString,
        string sql,
        object? parameters = null,
        int maxRows = 100,
        CancellationToken cancellationToken = default);
}
