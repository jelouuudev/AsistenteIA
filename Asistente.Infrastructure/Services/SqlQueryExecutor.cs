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
        CancellationToken cancellationToken = default,
        int commandTimeoutSegundos = 30)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = Math.Max(1, commandTimeoutSegundos),
            CommandType = CommandType.Text
        };

        if (parameters is IEnumerable<KeyValuePair<string, object?>> paramPairs)
        {
            foreach (var kvp in paramPairs)
            {
                command.Parameters.AddWithValue(kvp.Key, NormalizarValor(kvp.Value) ?? DBNull.Value);
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

    // Los parametros que llegan por JSON se deserializan como JsonElement (object?);
    // SqlClient no los mapea: convertir a tipos CLR antes de agregarlos.
    private static object? NormalizarValor(object? valor)
    {
        if (valor is System.Text.Json.JsonElement elem)
        {
            return elem.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => elem.GetString(),
                System.Text.Json.JsonValueKind.Number => elem.TryGetInt64(out var l) ? l : elem.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined => null,
                _ => elem.GetRawText(),
            };
        }
        return valor;
    }
}
