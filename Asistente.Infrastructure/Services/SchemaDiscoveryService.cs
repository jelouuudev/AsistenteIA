using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Asistente.Infrastructure.Services;

public class SchemaDiscoveryService : ISchemaDiscoveryService
{
    private const string TablasQuery = @"
        SELECT SCHEMA_NAME(o.schema_id) AS Esquema,
               o.name AS Nombre,
               'TABLA' AS Tipo
        FROM sys.objects o
        WHERE o.type = 'U' AND o.is_ms_shipped = 0
        ORDER BY Esquema, Nombre;";

    private const string VistasQuery = @"
        SELECT SCHEMA_NAME(o.schema_id) AS Esquema,
               o.name AS Nombre,
               'VISTA' AS Tipo
        FROM sys.objects o
        WHERE o.type = 'V' AND o.is_ms_shipped = 0
        ORDER BY Esquema, Nombre;";

    private const string ColumnasQuery = @"
        SELECT SCHEMA_NAME(t.schema_id) AS Esquema,
               t.name AS Nombre,
               c.name AS Columna,
               ty.name AS TipoDato,
               c.max_length AS Longitud,
               c.is_nullable AS Anulable,
               CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS EsClavePrimaria,
               CASE WHEN fk.column_id IS NOT NULL THEN 1 ELSE 0 END AS EsClaveForanea
        FROM sys.tables t
        JOIN sys.columns c ON c.object_id = t.object_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN (
            SELECT ic.object_id, ic.column_id
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.is_primary_key = 1
        ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
        LEFT JOIN (
            SELECT fc.parent_object_id AS object_id, fc.parent_column_id AS column_id
            FROM sys.foreign_key_columns fc
        ) fk ON fk.object_id = c.object_id AND fk.column_id = c.column_id;";

    private const string RelacionesQuery = @"
        SELECT fk.name AS Nombre,
               SCHEMA_NAME(po.schema_id) + '.' + po.name AS TablaOrigen,
               pc.name AS ColumnaOrigen,
               SCHEMA_NAME(ro.schema_id) + '.' + ro.name AS TablaDestino,
               rc.name AS ColumnaDestino
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
        JOIN sys.objects po ON po.object_id = fc.parent_object_id
        JOIN sys.columns pc ON pc.object_id = fc.parent_object_id AND pc.column_id = fc.parent_column_id
        JOIN sys.objects ro ON ro.object_id = fc.referenced_object_id
        JOIN sys.columns rc ON rc.object_id = fc.referenced_object_id AND rc.column_id = fc.referenced_column_id;";

    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection.State == System.Data.ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    public async Task<EsquemaBaseDatosInfo> DiscoverAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var result = new EsquemaBaseDatosInfo();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        result.BaseDatos = connection.Database;

        var tablas = (await connection.QueryAsync(new CommandDefinition(TablasQuery, cancellationToken: cancellationToken)))
            .Select(r => new ObjetoEsquemaInfo
            {
                Esquema = r.Esquema,
                Nombre = r.Nombre,
                Tipo = r.Tipo
            })
            .ToList();

        var vistas = (await connection.QueryAsync(new CommandDefinition(VistasQuery, cancellationToken: cancellationToken)))
            .Select(r => new ObjetoEsquemaInfo
            {
                Esquema = r.Esquema,
                Nombre = r.Nombre,
                Tipo = r.Tipo
            })
            .ToList();

        var columnas = (await connection.QueryAsync(new CommandDefinition(ColumnasQuery, cancellationToken: cancellationToken))).ToList();

        var relaciones = (await connection.QueryAsync(new CommandDefinition(RelacionesQuery, cancellationToken: cancellationToken)))
            .Select(r => new RelacionEsquemaInfo
            {
                Nombre = r.Nombre,
                TablaOrigen = r.TablaOrigen,
                ColumnaOrigen = r.ColumnaOrigen,
                TablaDestino = r.TablaDestino,
                ColumnaDestino = r.ColumnaDestino
            })
            .ToList();

        foreach (var tabla in tablas)
        {
            tabla.Columnas = columnas
                .Where(c => string.Equals((string)c.Esquema, tabla.Esquema, StringComparison.OrdinalIgnoreCase)
                            && string.Equals((string)c.Nombre, tabla.Nombre, StringComparison.OrdinalIgnoreCase))
                .Select(c => new ColumnaEsquemaInfo
                {
                    Nombre = c.Columna,
                    TipoDato = c.TipoDato,
                    Longitud = c.Longitud is int len && len > 0 ? len : null,
                    Anulable = Convert.ToBoolean(c.Anulable),
                    EsClavePrimaria = Convert.ToBoolean(c.EsClavePrimaria),
                    EsClaveForanea = Convert.ToBoolean(c.EsClaveForanea)
                })
                .OrderBy(c => c.Nombre)
                .ToList();
        }

        foreach (var vista in vistas)
        {
            vista.Columnas = new List<ColumnaEsquemaInfo>();
        }

        result.Tablas = tablas;
        result.Vistas = vistas;
        result.Relaciones = relaciones;

        return result;
    }
}
