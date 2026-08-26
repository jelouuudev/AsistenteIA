using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Asistente.Infrastructure.Services;

public class SqlQueryExecutor : ISqlQueryExecutor
{
    public async Task<IEnumerable<Dictionary<string, object?>>> ExecuteReadOnlyAsync(
        string connectionString,
        string sql,
        object? parameters = null,
        int maxRows = 100,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 30,
            CommandType = CommandType.Text
        };

        if (parameters is IEnumerable<KeyValuePair<string, object?>> paramPairs)
        {
            foreach (var kvp in paramPairs)
            {
                command.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
            }
        }

        var result = new List<Dictionary<string, object?>>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (result.Count >= maxRows)
                break;

            var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            result.Add(row);
        }

        return result;
    }
}
