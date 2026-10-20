using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Infrastructure.Data;
using Asistente.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Backfill de cifrado (<c>DbInitializer.CifrarContenidoPreexistenteAsync</c>).
///
/// Es el unico paso que puede fallar en silencio: si la deteccion de "contenido
/// sin cifrar" no encuentra nada, no da error, no avisa, y las filas se quedan
/// en claro indefinidamente en un despliegue nuevo. Dos pruebas cubren eso.
///
/// Se inserta y se lee con el <c>DbConnection</c> crudo, a proposito: el punto
/// entero del conversor de EF es que pasa por el, asi que un INSERT leido y
/// escrito por EF ya saldria cifrado y la prueba no estaria midiendo nada.
/// </summary>
public class MemoryBackfillCifradoTests : IDisposable
{
    private const string Clave = "clave-solo-para-estos-tests";
    private const string EnClaro = "CONTENIDO EN CLARO: la prueba del backfill";

    private readonly DbConnection _conn;
    private readonly AsistenteDbContext _ctx;

    public MemoryBackfillCifradoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        _ctx = new AsistenteDbContext(
            new DbContextOptionsBuilder<AsistenteDbContext>()
                .UseSqlite(_conn)
                .Options,
            MemoryCifrador.DesdeClave(Clave));

        // EnsureCreated no sirve: el modelo declara nvarchar(max) para Contenido y
        // SQLite dice 'near "max": syntax error'. DDL a mano con TEXT, igual que
        // hace PlanStepRepositoryTests con las suyas.
        CrearEsquema();
    }

    private void CrearEsquema()
    {
        Ejecutar("""
            CREATE TABLE IF NOT EXISTS [MemoryScopes] (
                [IdScope] INTEGER NOT NULL CONSTRAINT [PK_MemoryScopes] PRIMARY KEY AUTOINCREMENT,
                [Codigo] TEXT NOT NULL,
                [Nombre] TEXT NOT NULL,
                [Descripcion] TEXT NULL,
                [Activo] INTEGER NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS [IX_MemoryScopes_Codigo] ON [MemoryScopes]([Codigo]);

            CREATE TABLE IF NOT EXISTS [MemoryRecords] (
                [IdMemory] INTEGER NOT NULL CONSTRAINT [PK_MemoryRecords] PRIMARY KEY AUTOINCREMENT,
                [Codigo] TEXT NOT NULL,
                [Titulo] TEXT NOT NULL,
                [Contenido] TEXT NOT NULL,
                [IdScope] INTEGER NOT NULL,
                [IdPropietario] INTEGER NULL,
                [IdCategory] INTEGER NULL,
                [Activo] INTEGER NOT NULL,
                [VersionActual] INTEGER NOT NULL,
                [RequierePermiso] INTEGER NOT NULL,
                [VectorId] TEXT NULL,
                [Indexada] INTEGER NOT NULL,
                [FechaExpiracion] TEXT NULL,
                [FechaCreacion] TEXT NOT NULL,
                [FechaActualizacion] TEXT NOT NULL,
                CONSTRAINT [FK_MemoryRecords_MemoryScopes_IdScope]
                    FOREIGN KEY ([IdScope]) REFERENCES [MemoryScopes] ([IdScope]) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS [IX_MemoryRecords_Codigo] ON [MemoryRecords]([Codigo]);
            CREATE INDEX IF NOT EXISTS [IX_MemoryRecords_IdScope_Activo] ON [MemoryRecords]([IdScope], [Activo]);

            CREATE TABLE IF NOT EXISTS [MemoryVersions] (
                [IdVersion] INTEGER NOT NULL CONSTRAINT [PK_MemoryVersions] PRIMARY KEY AUTOINCREMENT,
                [IdMemory] INTEGER NOT NULL,
                [Numero] INTEGER NOT NULL,
                [Contenido] TEXT NOT NULL,
                [Motivo] TEXT NULL,
                [IdUsuario] INTEGER NULL,
                [Fecha] TEXT NOT NULL,
                CONSTRAINT [FK_MemoryVersions_MemoryRecords_IdMemory]
                    FOREIGN KEY ([IdMemory]) REFERENCES [MemoryRecords] ([IdMemory]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS [IX_MemoryVersions_IdMemory_Numero]
                ON [MemoryVersions]([IdMemory], [Numero]);
            """);
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Backfill_CifraLasFilasQueQuedaronEnClaro()
    {
        InsertarEnClaro("BACKFILL-SIN-CIFRAR", EnClaro);
        InsertarVersionEnClaro("BACKFILL-VERSION", EnClaro);

        await DbInitializer.CifrarContenidoPreexistenteAsync(_ctx);

        var memorias = LeerCrudas("SELECT Contenido FROM MemoryRecords WHERE Codigo = 'BACKFILL-SIN-CIFRAR'");
        var versiones = LeerCrudas("SELECT Contenido FROM MemoryVersions WHERE IdVersion = (SELECT MAX(IdVersion) FROM MemoryVersions)");

        Assert.All(memorias, c =>
        {
            Assert.StartsWith(MemoryCifrador.Prefijo, c, StringComparison.Ordinal);
            Assert.DoesNotContain(EnClaro, c, StringComparison.Ordinal);
        });
        Assert.All(versiones, c =>
        {
            Assert.StartsWith(MemoryCifrador.Prefijo, c, StringComparison.Ordinal);
            Assert.DoesNotContain(EnClaro, c, StringComparison.Ordinal);
        });

        // Y el cifrado se puede abrir con la misma clave.
        Assert.Equal(EnClaro, MemoryCifrador.DesdeClave(Clave).Descifrar(memorias[0]));
    }

    [Fact]
    public async Task Backfill_EsIdempotente_NoVuelveATocarLoYaCifrado()
    {
        InsertarEnClaro("BACKFILL-IDEMPOTENTE", EnClaro);

        await DbInitializer.CifrarContenidoPreexistenteAsync(_ctx);
        var primera = LeerCrudas("SELECT Contenido FROM MemoryRecords WHERE Codigo = 'BACKFILL-IDEMPOTENTE'").Single();

        await DbInitializer.CifrarContenidoPreexistenteAsync(_ctx);
        var segunda = LeerCrudas("SELECT Contenido FROM MemoryRecords WHERE Codigo = 'BACKFILL-IDEMPOTENTE'").Single();

        // Un segundo barrido no debe reescribir: cada corrida genera otro IV, asi que
        // si se vuelve a cifrar la cadena cambia aunque el texto de dentro sea igual.
        Assert.Equal(primera, segunda);
    }

    [Fact]
    public void Linq_EnviaElArgumentoCifrado_PorEsoNoSirveParaDetectar()
    {
        var prefijo = MemoryCifrador.Prefijo;

        // Solo se traduce, no se ejecuta: con el cifrado dentro del ESCAPE el motor
        // exige un caracter unico y el propio EF acaba dando error en algunos.
        var traduccion = _ctx.MemoryRecords
            .Where(m => m.Contenido.StartsWith(prefijo))
            .ToQueryString();

        // EF pasa por el MISMO conversor el argumento de la consulta que la columna,
        // asi que el predicado compara contra enc(v1:...) y jamas puede casar con un
        // contenido. Con el NOT de la deteccion eso no da cero filas: da todas.
        //
        // El prefijo no puede aparecer como valor literal. Si en alguna version
        // empezara a aparecer entre comillas propias, LINQ volveria a servir para
        // detectar y este test, y la decision de usar SQL crudo, habrian caducado.
        Assert.DoesNotContain($"'{prefijo}'", traduccion, StringComparison.Ordinal);
        Assert.Contains(prefijo, traduccion, StringComparison.Ordinal);
    }

    // Se escribe con el DbConnection, sin pasar por EF: un INSERT hecho con el
    // contexto saldria ya cifrado y no habria nada que reparar.
    private void InsertarEnClaro(string codigo, string contenido)
    {
        Ejecutar(
            @"INSERT INTO MemoryScopes (Codigo, Nombre, Descripcion, Activo)
              VALUES (@codigo, 'Scope de prueba', NULL, 1)",
            ("@codigo", "S-" + codigo));
        var idScope = (long)ExecScalar("SELECT last_insert_rowid()");

        Ejecutar(
            @"INSERT INTO MemoryRecords
                (Codigo, Titulo, Contenido, IdScope, IdPropietario, IdCategory, Activo,
                 VersionActual, RequierePermiso, VectorId, Indexada, FechaExpiracion,
                 FechaCreacion, FechaActualizacion)
              VALUES (@codigo, 'Titulo de prueba', @contenido, @scope, NULL, NULL, 1,
                      1, 0, NULL, 0, NULL, datetime('now'), datetime('now'))",
            ("@codigo", codigo), ("@contenido", contenido), ("@scope", idScope));
    }

    private void InsertarVersionEnClaro(string codigo, string contenido)
    {
        InsertarEnClaro(codigo, contenido);
        var idMemory = (long)ExecScalar("SELECT last_insert_rowid()");

        Ejecutar(
            @"INSERT INTO MemoryVersions (IdMemory, Numero, Contenido, Motivo, IdUsuario, Fecha)
              VALUES (@id, 1, @contenido, 'sin cifrar', NULL, datetime('now'))",
            ("@id", idMemory), ("@contenido", contenido));
    }

    private string[] LeerCrudas(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var valores = new System.Collections.Generic.List<string>();
        while (reader.Read()) valores.Add(reader.GetString(0));
        return valores.ToArray();
    }

    private void Ejecutar(string sql, params (string Nombre, object Valor)[] parametros)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = nombre;
            p.Value = valor;
            cmd.Parameters.Add(p);
        }
        cmd.ExecuteNonQuery();
    }

    private object ExecScalar(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()!;
    }
}
