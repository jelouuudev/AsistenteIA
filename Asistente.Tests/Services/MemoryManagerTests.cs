using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Herramientas;
using Asistente.Application.Services.Memoria;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Asistente.Tests.Services;

/// <summary>Pruebas de la ETAPA 21: manager, versionado, retención, búsqueda,
/// permisos, auditoría, orchestrator y tool. Todo con dobles en memoria.</summary>
public class MemoryManagerTests
{
    private sealed class RepoMemoria : IMemoryRepository
    {
        public List<MemoryRecord> Datos { get; } = new();
        public List<MemoryScope> Scopes { get; } = new();
        public List<MemoryCategory> Categorias { get; } = new();
        public List<MemoryRetentionPolicy> Politicas { get; } = new();
        private int _seq = 0;

        public RepoMemoria()
        {
            Scopes.AddRange(new[]
            {
                new MemoryScope { IdScope = 1, Codigo = "Usuario", Nombre = "Usuario", Activo = true },
                new MemoryScope { IdScope = 2, Codigo = "Organizacion", Nombre = "Organización", Activo = true },
                new MemoryScope { IdScope = 3, Codigo = "Workflow", Nombre = "Workflow", Activo = true },
                new MemoryScope { IdScope = 4, Codigo = "Agente", Nombre = "Agente", Activo = true },
                new MemoryScope { IdScope = 5, Codigo = "Conversacional", Nombre = "Conversacional", Activo = true }
            });
            Categorias.Add(new MemoryCategory { IdCategory = 1, Codigo = "FormatoReporte", Nombre = "Formato", Activo = true });
        }

        private void Vincular(MemoryRecord m)
        {
            m.Scope = Scopes.FirstOrDefault(s => s.IdScope == m.IdScope);
            m.Category = Categorias.FirstOrDefault(c => c.IdCategory == m.IdCategory);
        }

        public Task<MemoryRecord?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var m = Datos.FirstOrDefault(x => x.IdMemory == id);
            if (m != null) Vincular(m);
            return Task.FromResult(m);
        }

        public Task<MemoryRecord?> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        {
            var m = Datos.FirstOrDefault(x => x.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));
            if (m != null) Vincular(m);
            return Task.FromResult(m);
        }

        public Task<List<MemoryRecord>> GetActivasAsync(CancellationToken ct = default)
        {
            var lista = Datos.Where(x => x.Activo).ToList();
            lista.ForEach(Vincular);
            return Task.FromResult(lista);
        }

        public Task<List<MemoryRecord>> GetByScopeAsync(int idScope, CancellationToken ct = default)
        {
            var lista = Datos.Where(x => x.IdScope == idScope && x.Activo).ToList();
            lista.ForEach(Vincular);
            return Task.FromResult(lista);
        }

        public Task<MemoryRecord> AddAsync(MemoryRecord m, CancellationToken ct = default)
        {
            m.IdMemory = ++_seq;
            Datos.Add(m);
            Vincular(m);
            return Task.FromResult(m);
        }

        public Task UpdateAsync(MemoryRecord m, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(MemoryRecord m, CancellationToken ct = default)
        {
            Datos.Remove(m);
            return Task.CompletedTask;
        }
        public Task<List<MemoryScope>> GetScopesAsync(CancellationToken ct = default)
            => Task.FromResult(Scopes.Where(s => s.Activo).ToList());
        public Task<List<MemoryCategory>> GetCategoriesAsync(CancellationToken ct = default)
            => Task.FromResult(Categorias.Where(c => c.Activo).ToList());
        public Task<List<MemoryRetentionPolicy>> GetPoliticasAsync(CancellationToken ct = default)
            => Task.FromResult(Politicas.Where(p => p.Activo).ToList());
    }

    private sealed class RepoVersiones : IMemoryVersionRepository
    {
        public List<MemoryVersion> Datos { get; } = new();
        public Task AddAsync(MemoryVersion v, CancellationToken ct = default)
        {
            Datos.Add(v);
            return Task.CompletedTask;
        }
        public Task<List<MemoryVersion>> GetByMemoryAsync(int id, CancellationToken ct = default)
            => Task.FromResult(Datos.Where(v => v.IdMemory == id).OrderBy(v => v.Numero).ToList());
        public Task<MemoryVersion?> GetByNumeroAsync(int id, int numero, CancellationToken ct = default)
            => Task.FromResult(Datos.FirstOrDefault(v => v.IdMemory == id && v.Numero == numero));
    }

    private sealed class RepoLogs : IMemoryAccessLogRepository
    {
        public List<MemoryAccessLog> Datos { get; } = new();
        public Task AddAsync(MemoryAccessLog l, CancellationToken ct = default)
        {
            Datos.Add(l);
            return Task.CompletedTask;
        }
        public Task<List<MemoryAccessLog>> GetByMemoryAsync(int id, int tope = 100, CancellationToken ct = default)
            => Task.FromResult(Datos.Where(l => l.IdMemory == id).Take(tope).ToList());
        public Task<long> ContarPorOperacionAsync(string op, CancellationToken ct = default)
            => Task.FromResult(Datos.LongCount(l => l.Operacion == op));

        // Observabilidad: se calcula en memoria como lo haria el GROUP BY de SQL.
        public Task<List<(int? Agente, long Total)>> ContarPorAgenteAsync(CancellationToken ct = default)
            => Task.FromResult(Datos.Where(l => l.IdAsistente != null)
                .GroupBy(l => l.IdAsistente!.Value)
                .Select(g => ((int?)g.Key, (long)g.LongCount()))
                .ToList());

        public Task<List<(int? Usuario, long Total)>> ContarPorUsuarioAsync(CancellationToken ct = default)
            => Task.FromResult(Datos.Where(l => l.IdUsuario != null)
                .GroupBy(l => l.IdUsuario!.Value)
                .Select(g => ((int?)g.Key, (long)g.LongCount()))
                .ToList());

        public Task<double?> DuracionMediaMsAsync(string op, CancellationToken ct = default)
        {
            var conTiempo = Datos.Where(l => l.Operacion == op && l.DuracionMs != null)
                .Select(l => (double)l.DuracionMs!.Value).ToList();
            return Task.FromResult<double?>(conTiempo.Count == 0 ? null : conTiempo.Average());
        }

        public Task<List<(string? Ambito, long Total)>> ContarPorTipoAsync(CancellationToken ct = default)
            => Task.FromResult(Datos.Where(l => l.Ambito != null)
                .GroupBy(l => l.Ambito!)
                .Select(g => ((string?)g.Key, (long)g.LongCount()))
                .ToList());
    }

    private sealed class VectorFalso : IVectorStore
    {
        public Dictionary<Guid, (string Texto, float[] Vec)> Indice { get; } = new();
        private static float[] Vec(string texto)
        {
            var v = new float[4];
            v[Math.Abs(texto.GetHashCode(StringComparison.Ordinal)) % 4] = 1f;
            return v;
        }
        public Task IndexAsync(VectorDocument d)
        {
            Indice[d.DocumentId] = (d.Text, d.Embedding);
            return Task.CompletedTask;
        }
        public Task IndexBatchAsync(IEnumerable<VectorDocument> ds)
        {
            foreach (var d in ds) Indice[d.DocumentId] = (d.Text, d.Embedding);
            return Task.CompletedTask;
        }
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK)
        {
            var palabras = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length >= 4).Select(p => p.ToLowerInvariant()).ToList();
            var hits = Indice
                .Where(kv => palabras.Any(p => kv.Value.Texto.ToLowerInvariant().Contains(p)))
                .Take(topK)
                .Select(kv => new VectorSearchResult
                {
                    DocumentId = kv.Key,
                    Text = kv.Value.Texto,
                    Score = 0.9f,
                    MetadataDocumentoNombre = "MEMORIA:X"
                });
            return Task.FromResult(hits);
        }
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int k, VectorSearchFilter f)
            => SearchAsync(q, k);
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int k, string n)
            => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync()
            => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id)
            => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync()
            => Task.FromResult(Enumerable.Empty<int>());
        public Task DeleteDocumentAsync(Guid id)
        {
            Indice.Remove(id);
            return Task.CompletedTask;
        }
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(Indice.Count);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task<string> GetDocumentTextAsync(int id) => Task.FromResult(string.Empty);
        public Task ClearAsync()
        {
            Indice.Clear();
            return Task.CompletedTask;
        }
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync()
            => Task.FromResult(Enumerable.Empty<(string, int)>());
    }

    private sealed class EmbeddingsFalsos : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text)
            => Task.FromResult(new float[] { 1f, 0f, 0f, 0f });
    }

    private static Mock<IAutorizacionService> Auth(params string[] permitidos)
    {
        var concedidos = new HashSet<string>(permitidos) { MemoryManager.PermisoAdministrarMemoria };
        var m = new Mock<IAutorizacionService>();
        m.Setup(a => a.VerificarPermisoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string codigo, CancellationToken _) => new ResultadoAutorizacion
            {
                Permitido = concedidos.Contains(codigo)
            });
        return m;
    }

    private static Mock<IAutorizacionService> AuthSinPermisoCompartido(params string[] permitidos)
    {
        var concedidos = new HashSet<string>(permitidos);
        var m = new Mock<IAutorizacionService>();
        m.Setup(a => a.VerificarPermisoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string codigo, CancellationToken _) => new ResultadoAutorizacion
            {
                Permitido = concedidos.Contains(codigo)
            });
        return m;
    }

    /// <summary>
    /// Regla de negocio: la memoria organizacional no se crea sin autorización.
    /// Antes CrearAsync era el único método sin comprobación de permiso y cualquier
    /// usuario autenticado podía escribir en la memoria de la organización.
    /// </summary>
    [Fact]
    public async Task Crear_EnAmbitoCompartido_SinPermiso_EsDenegadoYAuditado()
    {
        var repo = new RepoMemoria();
        var manager = new MemoryManager(repo,
            new MemoryVersionManager(new RepoVersiones()),
            new RepoLogs(),
            new SemanticMemorySearch(new VectorFalso(), new EmbeddingsFalsos(), NullLogger<SemanticMemorySearch>.Instance),
            new MemoryRetentionEngine(repo, new RepoLogs(), null!, NullLogger<MemoryRetentionEngine>.Instance),
            AuthSinPermisoCompartido().Object,
            NullLogger<MemoryManager>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "ORG-NO-AUTORIZADA",
            Titulo = "No autorizada",
            Contenido = "contenido",
            Ambito = "Organizacion"
        }, 9));

        Assert.Empty(repo.Datos);
    }

/// <summary>El ámbito Usuario propio sigue siendo autoservicio: sin permiso.</summary>
    [Fact]
    public async Task Crear_EnAmbitoUsuario_Propio_NoExigePermiso()
    {
        var (manager, repo, _, logs, _, _) = Fabrica(Array.Empty<string>());

        var m = await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "MIA",
            Titulo = "Mi preferencia",
            Contenido = "contenido",
            Ambito = "Usuario",
            IdPropietario = 7
        }, 7);

        Assert.Equal("MIA", m.Codigo);
        Assert.Single(repo.Datos);
        Assert.Contains(logs.Datos, l => l.Operacion == "Crear" && l.Resultado == "Exitoso");
    }

    /// <summary>
    /// El tipo de memoria debe quedar en la auditoría. Antes solo se guardaba el
    /// código, y al purgar una memoria IdMemory queda en null: el ámbito se perdía
    /// justo en el caso donde más importa la trazabilidad.
    /// </summary>
    [Fact]
    public async Task Auditoria_RegistraElTipoDeMemoria()
    {
        var (manager, repo, _, logs, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "CON-AMBITO",
            Titulo = "Con ámbito",
            Contenido = "x",
            Ambito = "Organizacion",
            RequierePermiso = false
        }, 1);
        await manager.ObtenerAsync("CON-AMBITO", 1);

        Assert.All(logs.Datos.Where(l => l.Codigo == "CON-AMBITO"),
            l => Assert.Equal("Organizacion", l.Ambito));
    }

    /// <summary>
    /// La purga borra la fila de la memoria: el evento de auditoría debe conservar
    /// el tipo de memoria para que la trazabilidad no se quede coja.
    /// </summary>
    [Fact]
    public async Task Auditoria_LaPurgaConservaElTipoDeMemoria()
    {
        var (manager, repo, _, logs, _, retencion) = Fabrica(Array.Empty<string>());
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdPolicy = 1, IdScope = null, DiasRetencion = 10,
            EliminarAlExpirar = true, ConservarTrazabilidad = true, Activo = true
        });
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "PURGA-AMBITO", Titulo = "x", Contenido = "y",
            Ambito = "Organizacion", RequierePermiso = false,
            FechaExpiracion = DateTime.UtcNow.AddDays(-1)
        }, 1);

        await retencion.AplicarAsync(1);

        var purga = logs.Datos.First(l => l.Operacion == "Purgar");
        Assert.Null(purga.IdMemory);
        Assert.Equal("PURGA-AMBITO", purga.Codigo);
        Assert.Equal("Organizacion", purga.Ambito);
    }

    /// <summary>
    /// Observabilidad: el panel debe exponer tiempo de recuperación, uso por agente,
    /// uso por usuario y accesos por tipo de memoria.
    /// </summary>
    [Fact]
    public async Task Metricas_ExponeTiempoDeRecuperacionYUsoPorAgenteYUsuario()
    {
        var (manager, repo, _, logs, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "ORG-1", Titulo = "x", Contenido = "contenido compartido largo",
            Ambito = "Organizacion", RequierePermiso = false
        }, 1);

        await manager.BuscarAsync("contenido compartido", 1, idAsistente: 5, topK: 5, ambito: null, ct: default);
        await manager.BuscarAsync("contenido compartido", 1, idAsistente: 5, topK: 5, ambito: null, ct: default);
        await manager.ObtenerAsync("ORG-1", 1);

        var m = await manager.MetricasAsync();

        Assert.True(m.Accesos > 0);
        Assert.Equal(m.Accesos, m.MemoriasConsultadas);
        Assert.NotNull(m.TiempoRecuperacionMs);
        Assert.True(m.TiempoRecuperacionMs >= 0);
        Assert.True(m.UsoPorUsuario.ContainsKey(1));
        // 1 Crear + 2 Buscar + 1 Leer: toda operación del usuario queda contada.
        Assert.Equal(4, m.UsoPorUsuario[1]);
        Assert.True(m.AccesosPorTipo.ContainsKey("Organizacion"));
    }

    /// <summary>
    /// Una búsqueda sin resultados también queda registrada con su duración: si no,
    /// el tiempo de recuperación solo mediría el camino feliz.
    /// </summary>
    [Fact]
    public async Task Metricas_LaBusquedaRegistraDuracion_InclusoSinResultados()
    {
        var (manager, _, _, logs, _, _) = Fabrica(Array.Empty<string>());

        await manager.BuscarAsync("nada de esto existe", 1, idAsistente: null, topK: 5, ambito: null, ct: default);
        var m = await manager.MetricasAsync();

        var busqueda = logs.Datos.First(l => l.Operacion == "Buscar");
        Assert.NotNull(busqueda.DuracionMs);
        Assert.NotNull(m.TiempoRecuperacionMs);
    }

    /// <summary>
    /// Autorización de escritura: los ámbitos compartidos exigen
    /// MEMORIA_ADMINISTRAR y el deniego queda auditado.
    /// </summary>
    [Fact]
    public async Task Crear_SinPermisoCompartido_QuedaAuditadoComoDenegado()
    {
        var repo = new RepoMemoria();
        var logs = new RepoLogs();
        var busqueda = new SemanticMemorySearch(new VectorFalso(), new EmbeddingsFalsos(), NullLogger<SemanticMemorySearch>.Instance);
        var manager = new MemoryManager(repo,
            new MemoryVersionManager(new RepoVersiones()),
            logs,
            busqueda,
            new MemoryRetentionEngine(repo, logs, busqueda, NullLogger<MemoryRetentionEngine>.Instance),
            AuthSinPermisoCompartido().Object,
            NullLogger<MemoryManager>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "ORG-DENEGADA", Titulo = "x", Contenido = "y", Ambito = "Organizacion"
        }, 4));

        Assert.Contains(logs.Datos, l =>
            l.Operacion == "Crear" && l.Resultado == "Denegado"
            && l.Motivo!.Contains(MemoryManager.PermisoAdministrarMemoria));
    }

    private static (MemoryManager Manager, RepoMemoria Repo, RepoVersiones Versiones,
        RepoLogs Logs, VectorFalso Vectores, MemoryRetentionEngine Retencion) Fabrica(
        string[] permitidos, bool conVectores = true)
    {
        var repo = new RepoMemoria();
        var versiones = new RepoVersiones();
        var logs = new RepoLogs();
        var vectores = new VectorFalso();
        IVectorStore store = conVectores
            ? vectores
            : new VectorRoto();
        var busqueda = new SemanticMemorySearch(store, new EmbeddingsFalsos(),
            NullLogger<SemanticMemorySearch>.Instance);
        var retencion = new MemoryRetentionEngine(repo, logs, busqueda,
            NullLogger<MemoryRetentionEngine>.Instance);
        var gestorVersiones = new MemoryVersionManager(versiones);
        var manager = new MemoryManager(repo, gestorVersiones, logs, busqueda, retencion,
            Auth(permitidos).Object, NullLogger<MemoryManager>.Instance);
        return (manager, repo, versiones, logs, vectores, retencion);
    }

    /// <summary>Vector store caído: prueba la degradación sin romper.</summary>
    private sealed class VectorRoto : IVectorStore
    {
        public Task IndexAsync(VectorDocument d) => throw new InvalidOperationException("caído");
        public Task IndexBatchAsync(IEnumerable<VectorDocument> ds) => throw new InvalidOperationException("caído");
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string q, int k) => throw new InvalidOperationException("caído");
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int k, VectorSearchFilter f) => throw new InvalidOperationException("caído");
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int k, string n) => throw new InvalidOperationException("caído");
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => throw new InvalidOperationException("caído");
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id) => throw new InvalidOperationException("caído");
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync() => throw new InvalidOperationException("caído");
        public Task DeleteDocumentAsync(Guid id) => throw new InvalidOperationException("caído");
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => throw new InvalidOperationException("caído");
        public Task<int> GetDocumentCountAsync() => throw new InvalidOperationException("caído");
        public Task<bool> HealthCheckAsync() => Task.FromResult(false);
        public Task<string> GetDocumentTextAsync(int id) => throw new InvalidOperationException("caído");
        public Task ClearAsync() => throw new InvalidOperationException("caído");
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync() => throw new InvalidOperationException("caído");
    }

    private static CrearMemoryRequest Nueva(string codigo = "MEM-1", string ambito = "Organizacion",
        string contenido = "Formato: encabezado, tabla, totales.", bool permiso = false) => new()
    {
        Codigo = codigo,
        Titulo = $"Título {codigo}",
        Contenido = contenido,
        Ambito = ambito,
        Categoria = "FormatoReporte",
        RequierePermiso = permiso
    };

    [Fact]
    public async Task Crear_GeneraV1_Indexa_YAudita()
    {
        var (manager, _, _, logs, vectores, _) = Fabrica(Array.Empty<string>());

        var dto = await manager.CrearAsync(Nueva(), 1);

        Assert.Equal(1, dto.VersionActual);
        Assert.True(dto.Indexada);
        Assert.Single(vectores.Indice);
        Assert.Contains(logs.Datos, l => l.Operacion == "Crear" && l.Resultado == "Exitoso");
    }

    [Fact]
    public async Task Crear_CodigoDuplicado_Falla()
    {
        var (manager, _, _, _, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(Nueva(), 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.CrearAsync(Nueva(), 1));
    }

    [Fact]
    public async Task Actualizar_CreaV2_ConservandoV1()
    {
        var (manager, _, versiones, _, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(Nueva(contenido: "original"), 1);

        var dto = await manager.ActualizarAsync("MEM-1",
            new ActualizarMemoryRequest { Contenido = "nuevo", Motivo = "mejora" }, 1);

        Assert.Equal(2, dto.VersionActual);
        Assert.Equal("nuevo", dto.Contenido);
        var historia = await manager.VersionesAsync("MEM-1", 1);
        Assert.Equal(2, historia.Count);
        Assert.Equal("original", historia[0].Contenido);
        Assert.Equal("nuevo", historia[1].Contenido);
        Assert.Equal("mejora", historia[1].Motivo);
    }

    [Fact]
    public async Task Revertir_AgregaV3SinReescribirHistoria()
    {
        var (manager, _, _, _, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(Nueva(contenido: "v1"), 1);
        await manager.ActualizarAsync("MEM-1",
            new ActualizarMemoryRequest { Contenido = "v2" }, 1);

        var dto = await manager.RevertirAsync("MEM-1", 1, "error en v2", 1);

        Assert.Equal(3, dto.VersionActual);
        Assert.Equal("v1", dto.Contenido);
        var historia = await manager.VersionesAsync("MEM-1", 1);
        Assert.Equal(3, historia.Count);
        Assert.Equal("v2", historia[1].Contenido);
    }

    [Fact]
    public async Task Leer_SinPermiso_NiegaSinExponerContenido()
    {
        var (manager, _, _, logs, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(Nueva(permiso: true), 1);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            manager.ObtenerAsync("MEM-1", 9));
        Assert.Contains("MEMORIA:MEM-1", ex.Message);
        var denegado = Assert.Single(logs.Datos.Where(l => l.Resultado == "Denegado"));
        Assert.Equal("Leer", denegado.Operacion);
    }

    [Fact]
    public async Task Leer_ConPermiso_DevuelveContenido()
    {
        var (manager, _, _, _, _, _) = Fabrica(new[] { "MEMORIA:MEM-1" });
        await manager.CrearAsync(Nueva(permiso: true), 1);

        var dto = await manager.ObtenerAsync("MEM-1", 9);

        Assert.Equal("Formato: encabezado, tabla, totales.", dto.Contenido);
    }

    [Fact]
    public async Task Leer_PropietarioAmbitoUsuario_SinPermisoGlobal()
    {
        var (manager, _, _, _, _, _) = Fabrica(Array.Empty<string>());
        var dto0 = await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "MIA",
            Titulo = "Mía",
            Contenido = "secreto personal",
            Ambito = "Usuario",
            IdPropietario = 5,
            RequierePermiso = true
        }, 5);

        var dto = await manager.ObtenerAsync("MIA", 5);

        Assert.Equal("secreto personal", dto.Contenido);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            manager.ObtenerAsync("MIA", 6));
        Assert.Equal("MIA", dto0.Codigo);
    }

    [Fact]
    public async Task Buscar_Semantica_RespetaPermisos()
    {
        var (manager, _, _, _, _, _) = Fabrica(new[] { "MEMORIA:PUB" });
        await manager.CrearAsync(Nueva("PUB", "Organizacion", "formato mensual de reportes", false), 1);
        await manager.CrearAsync(Nueva("PRIV", "Organizacion", "formato mensual secreto", true), 1);

        var hits = await manager.BuscarAsync("formato mensual reportes", 9);

        Assert.Single(hits);
        Assert.Equal("PUB", hits[0].Codigo);
    }

    [Fact]
    public async Task Buscar_SinVector_DegradaATextoSinRomper()
    {
        var (manager, _, _, _, _, _) = Fabrica(Array.Empty<string>(), conVectores: false);
        await manager.CrearAsync(Nueva("T1", "Organizacion", "formato mensual de reportes", false), 1);

        var hits = await manager.BuscarAsync("formato mensual", 1);

        Assert.Single(hits);
        Assert.Equal("T1", hits[0].Codigo);
    }

    [Fact]
    public async Task Leer_Expirada_NoExisteYAudita()
    {
        var (manager, _, _, logs, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "VIEJA",
            Titulo = "Vieja",
            Contenido = "x",
            Ambito = "Organizacion",
            RequierePermiso = false,
            FechaExpiracion = DateTime.UtcNow.AddDays(-1)
        }, 1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.ObtenerAsync("VIEJA", 1));
        Assert.Contains(logs.Datos, l => l.Resultado == "Expirada");
    }

    [Fact]
    public async Task Retencion_DesactivaYPurgaSegunPolitica()
    {
        var (manager, repo, _, logs, _, retencion) = Fabrica(Array.Empty<string>());
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdPolicy = 1, IdScope = null, DiasRetencion = 10,
            EliminarAlExpirar = false, ConservarTrazabilidad = true, Activo = true
        });
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "CADUCA",
            Titulo = "Caduca",
            Contenido = "x",
            Ambito = "Organizacion",
            RequierePermiso = false,
            FechaExpiracion = DateTime.UtcNow.AddDays(-1)
        }, 1);

        var (des, eli) = await retencion.AplicarAsync(1);

        Assert.Equal(1, des);
        Assert.Equal(0, eli);
        Assert.Contains(logs.Datos, l => l.Operacion == "Expirar");

        repo.Politicas.First().EliminarAlExpirar = true;
        var m2 = await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "CADUCA2",
            Titulo = "Caduca2",
            Contenido = "y",
            Ambito = "Organizacion",
            RequierePermiso = false,
            FechaExpiracion = DateTime.UtcNow.AddDays(-1)
        }, 1);
        var (des2, eli2) = await retencion.AplicarAsync(1);

        Assert.Equal(0, des2);
        Assert.Equal(1, eli2);
        Assert.DoesNotContain(repo.Datos, x => x.Codigo == "CADUCA2");
        Assert.Contains(logs.Datos, l => l.Operacion == "Purgar" && l.Codigo == "CADUCA2");
        Assert.Equal(m2.Codigo, "CADUCA2");
    }

    [Fact]
    public async Task Eliminar_ConTrazabilidad_DesactivaSinBorrar()
    {
        var (manager, repo, _, logs, _, _) = Fabrica(Array.Empty<string>());
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdPolicy = 1, IdScope = null, DiasRetencion = 365,
            EliminarAlExpirar = false, ConservarTrazabilidad = true, Activo = true
        });
        await manager.CrearAsync(Nueva(permiso: false), 1);

        await manager.EliminarAsync("MEM-1", 1);

        Assert.Contains(repo.Datos, x => x.Codigo == "MEM-1" && !x.Activo);
        Assert.Contains(logs.Datos, l => l.Operacion == "Eliminar");
    }

    private static Mock<IMemoriaService> Conversaciones(
        Dictionary<int, ConversacionDto>? conversaciones = null)
    {
        var mock = new Mock<IMemoriaService>();
        mock.Setup(c => c.ObtenerConversacionAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => conversaciones != null && conversaciones.TryGetValue(id, out var conv)
                ? conv
                : null);
        return mock;
    }

    private static MemoryOrchestrator Orquestador(MemoryManager manager, RepoMemoria repo,
        Dictionary<int, ConversacionDto>? conversaciones = null)
        => new(manager, repo, Conversaciones(conversaciones).Object,
            NullLogger<MemoryOrchestrator>.Instance);

    [Fact]
    public async Task Orchestrator_OrdenaWorkflowAgenteUsuarioOrg_YRecorta()
    {
        var (manager, repo, _, _, _, _) = Fabrica(new[] { "MEMORIA:O", "MEMORIA:A", "MEMORIA:U", "MEMORIA:W" });
        var crear = async (string codigo, string ambito, int? prop, string contenido) =>
            await manager.CrearAsync(new CrearMemoryRequest
            {
                Codigo = codigo, Titulo = codigo, Contenido = contenido,
                Ambito = ambito, IdPropietario = prop, RequierePermiso = true
            }, 7);
        await crear("O", "Organizacion", null, new string('o', 500));
        await crear("A", "Agente", 9, new string('a', 500));
        await crear("U", "Usuario", 7, new string('u', 500));
        await crear("W", "Workflow", 3, new string('w', 500));
        var orch = Orquestador(manager, repo);

        var ctx = await orch.ObtenerContextoAsync(7, 9, 3, 3000);

        var iW = ctx.IndexOf("[Memoria Workflow", StringComparison.Ordinal);
        var iA = ctx.IndexOf("[Memoria Agente", StringComparison.Ordinal);
        var iU = ctx.IndexOf("[Memoria Usuario", StringComparison.Ordinal);
        var iO = ctx.IndexOf("[Memoria Organizacion", StringComparison.Ordinal);
        Assert.True(iW >= 0 && iW < iA && iA < iU && iU < iO);
        Assert.True(ctx.Length <= 3000);
    }

    [Fact]
    public async Task Orchestrator_RespetaTope_DejandoLoPrioritario()
    {
        var (manager, repo, _, _, _, _) = Fabrica(new[] { "MEMORIA:O", "MEMORIA:W" });
        var crear = async (string codigo, string ambito, int? prop, string contenido) =>
            await manager.CrearAsync(new CrearMemoryRequest
            {
                Codigo = codigo, Titulo = codigo, Contenido = contenido,
                Ambito = ambito, IdPropietario = prop, RequierePermiso = true
            }, 7);
        await crear("O", "Organizacion", null, new string('o', 500));
        await crear("W", "Workflow", 3, new string('w', 500));
        var orch = Orquestador(manager, repo);

        // Solo cabe el bloque Workflow (~520): Organización queda fuera.
        var ctx = await orch.ObtenerContextoAsync(7, null, 3, 600);

        Assert.Contains("[Memoria Workflow", ctx);
        Assert.DoesNotContain("[Memoria Organizacion", ctx);
        Assert.True(ctx.Length <= 600);
    }

    [Fact]
    public async Task Orchestrator_Workflow_GuardaYRetomaEstado()
    {
        var (manager, repo, _, _, _, _) = Fabrica(Array.Empty<string>());
        var orch = Orquestador(manager, repo);

        await orch.GuardarEstadoWorkflowAsync(11, "WF-11", "Estado", "paso=2", 7);
        var leida = await orch.LeerEstadoWorkflowAsync(11, "WF-11", 7);
        await orch.GuardarEstadoWorkflowAsync(11, "WF-11", "Estado", "paso=3", 7);
        var versiones = await manager.VersionesAsync("WF-11", 7);

        Assert.Equal("paso=2", leida.Contenido);
        Assert.Equal(2, versiones.Count);
        Assert.Equal("paso=3", (await manager.ObtenerAsync("WF-11", 7)).Contenido);
    }

    [Fact]
    public async Task MemoryTool_BuscarLeerGuardar_RespetanPermiso()
    {
        var (manager, _, _, _, _, _) = Fabrica(new[] { "MEMORIA:PUB" });
        await manager.CrearAsync(Nueva("PUB", "Organizacion", "formato mensual", false), 1);
        await manager.CrearAsync(Nueva("PRIV", "Organizacion", "secreto", true), 1);
        var tool = new MemoryTool(manager);

        var buscar = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "MemoryTool",
            Parametros = new Dictionary<string, object?>
                { ["operacion"] = "buscar", ["texto"] = "formato mensual" },
            IdUsuario = 9
        });
        Assert.True(buscar.Exitoso);
        Assert.Contains("PUB", buscar.Contenido);
        Assert.DoesNotContain("secreto", buscar.Contenido);

        var leerNegado = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "MemoryTool",
            Parametros = new Dictionary<string, object?> { ["operacion"] = "leer", ["codigo"] = "PRIV" },
            IdUsuario = 9
        });
        Assert.False(leerNegado.Exitoso);

        var guardar = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "MemoryTool",
            Parametros = new Dictionary<string, object?>
            {
                ["operacion"] = "guardar", ["codigo"] = "NUEVA",
                ["contenido"] = "dato", ["ambito"] = "Usuario"
            },
            IdUsuario = 9
        });
        Assert.True(guardar.Exitoso);
    }

    [Fact]
    public async Task Orchestrator_Conversacional_SoloPropietarioYActiva()
    {
        var (manager, repo, _, _, _, _) = Fabrica(Array.Empty<string>());
        var convs = new Dictionary<int, ConversacionDto>
        {
            [77] = new ConversacionDto
            {
                IdConversacion = 77,
                UsuarioPropietario = 7,
                Estado = "Activa",
                Titulo = "Soporte",
                Mensajes = new System.Collections.Generic.List<MensajeDto>
                {
                    new() { IdMensaje = 1, IdConversacion = 77, Rol = "user", Contenido = "mi impresora no imprime" },
                    new() { IdMensaje = 2, IdConversacion = 77, Rol = "assistant", Contenido = "revise el tóner" },
                    new() { IdMensaje = 3, IdConversacion = 77, Rol = "user", Contenido = "gracias, funcionó" }
                }
            }
        };
        var orch = Orquestador(manager, repo, convs);

        // Los últimos 2 turnos sí; el primero queda fuera del tope.
        var ctx = await orch.ObtenerContextoAsync(7, null, null, 3000, 77, 2);

        Assert.Contains("[Memoria Conversacional: Soporte]", ctx);
        Assert.Contains("assistant: revise el tóner", ctx);
        Assert.Contains("gracias, funcionó", ctx);
        Assert.DoesNotContain("impresora", ctx);

        // Conversación ajena: vacía, sin confirmar ni negar su existencia.
        var ajena = await orch.ObtenerContextoAsync(9, null, null, 3000, 77, 5);
        Assert.DoesNotContain("Memoria Conversacional", ajena);
    }

    /// <summary>
    /// Una memoria registrada en ámbito Conversacional debe llegar al contexto. Antes
    /// caía en la rama de descarte del selector de ámbito: el catálogo la aceptaba al
    /// crearse, pero jamás se inyectaba, así que era una memoria guardable e invisible.
    /// </summary>
    [Fact]
    public async Task Orchestrator_RegistraElAmbitoConversacional()
    {
        var (manager, repo, _, _, _, _) = Fabrica(Array.Empty<string>());
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "CONV-1",
            Titulo = "Incidencia recurrente",
            Contenido = "el tóner se agota cada dos semanas",
            Ambito = "Conversacional",
            IdPropietario = 7,
            RequierePermiso = false
        }, 7);
        var orch = Orquestador(manager, repo);

        var ctx = await orch.ObtenerContextoAsync(7, null, null, 3000);

        Assert.Contains("[Memoria Conversacional: Incidencia recurrente]", ctx);
        Assert.Contains("el tóner se agota", ctx);
    }

    /// <summary>
    /// El bloque conversacional (historial reconstruido) también debe respetar el
    /// presupuesto de caracteres: se antepone a todo lo demás, así que sin recorte
    /// un historial largo desbordaba el tope del prompt por sí solo.
    /// </summary>
    [Fact]
    public async Task Orchestrator_RespetarTope_InclusoConElHistorialDelante()
    {
        var (manager, repo, _, _, _, _) = Fabrica(Array.Empty<string>());
        var convs = new Dictionary<int, ConversacionDto>
        {
            [5] = new ConversacionDto
            {
                IdConversacion = 5,
                UsuarioPropietario = 7,
                Estado = "Activa",
                Titulo = "Larga",
                Mensajes = Enumerable.Range(1, 40)
                    .Select(i => new MensajeDto
                    {
                        IdMensaje = i,
                        IdConversacion = 5,
                        Rol = "user",
                        Contenido = $"mensaje largo numero {i} con relleno para alargar el historial"
                    })
                    .ToList()
            }
        };
        var orch = Orquestador(manager, repo, convs);

        var ctx = await orch.ObtenerContextoAsync(7, null, null, 800, 5, 40);

        Assert.True(ctx.Length <= 800, $"El contexto excedió el tope: {ctx.Length} caracteres.");
        Assert.Contains("[Memoria Conversacional: Larga]", ctx);
    }

    /// <summary>
    /// El estado de un workflow se relee aunque haya expirado. Antes se consultaba con
    /// ObtenerAsync, que además de permisos evalúa la retención: un estado de más de
    /// 365 días quedaba imposible de reescribir.
    /// </summary>
    [Fact]
    public async Task Orchestrator_ReescribeEstadoWorkflowVencido()
    {
        var (manager, repo, _, _, _, _) = Fabrica(Array.Empty<string>());
        repo.Politicas.Add(new MemoryRetentionPolicy
        {
            IdPolicy = 1, IdScope = 3, DiasRetencion = 10,
            EliminarAlExpirar = false, ConservarTrazabilidad = true, Activo = true
        });
        await manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = "WF-9",
            Titulo = "Estado",
            Contenido = "paso=1",
            Ambito = "Workflow",
            IdPropietario = 9,
            RequierePermiso = false,
            FechaExpiracion = DateTime.UtcNow.AddDays(-1)
        }, 7);
        var orch = Orquestador(manager, repo);

        var guardada = await orch.GuardarEstadoWorkflowAsync(9, "WF-9", "Estado", "paso=2", 7);

        Assert.Equal("paso=2", guardada.Contenido);
        Assert.Equal(2, guardada.VersionActual);

        // Sin estado previo todavía disponible (vencido): null, no excepción.
        Assert.Null(await orch.TryLeerEstadoWorkflowAsync("WF-NO-EXISTE", 7));
    }

    private static (MemoryVersionManager Gestor, RepoVersiones Repo) FabricaVersiones()
    {
        var repo = new RepoVersiones();
        return (new MemoryVersionManager(repo), repo);
    }

    private static MemoryRecord Memoria(int idMemory, int versionActual) => new()
    {
        IdMemory = idMemory,
        Codigo = "MEM",
        Titulo = "MEM",
        Contenido = $"contenido v{versionActual}",
        VersionActual = versionActual
    };

    /// <summary>
    /// La versión anexada lleva el número que la memoria declara vigente: el
    /// contador vive en un solo sitio y una versión nunca queda desalineada.
    /// </summary>
    [Fact]
    public async Task VersionManager_RegistraLaVersionDeclaradaVigente()
    {
        var (gestor, repo) = FabricaVersiones();

        var v = await gestor.RegistrarAsync(Memoria(7, 3), "contenido nuevo", "Actualización", 42);

        Assert.Equal(7, v.IdMemory);
        Assert.Equal(3, v.Numero);
        Assert.Equal("contenido nuevo", v.Contenido);
        Assert.Equal("Actualización", v.Motivo);
        Assert.Equal(42, v.IdUsuario);
        Assert.Single(repo.Datos);
    }

    /// <summary>
    /// Append-only real: tres escrituras dejan tres versiones y las anteriores no
    /// se tocan. Es el motivo de que revertir sea "copiar el contenido viejo como
    /// versión nueva" en vez de restaurar la fila.
    /// </summary>
    [Fact]
    public async Task VersionManager_HistorialEsAppendOnly()
    {
        var (gestor, repo) = FabricaVersiones();
        await gestor.RegistrarAsync(Memoria(1, 1), "uno", "Creación", 1);
        await gestor.RegistrarAsync(Memoria(1, 2), "dos", "Actualización", 1);
        await gestor.RegistrarAsync(Memoria(1, 3), "tres", "Actualización", 1);

        var historial = await gestor.ListarAsync(1);

        Assert.Equal(3, historial.Count);
        Assert.Equal(new[] { "uno", "dos", "tres" }, historial.Select(v => v.Contenido));
        Assert.Equal(new[] { 1, 2, 3 }, historial.Select(v => v.Numero));
        Assert.Equal(3, await gestor.ContarAsync(1));
    }

    /// <summary>
    /// Revertir copia el contenido de la versión elegida como versión nueva: la
    /// v1 queda intacta en el historial.
    /// </summary>
    [Fact]
    public async Task VersionManager_RevertirConservaLaVersionOriginal()
    {
        var (gestor, _) = FabricaVersiones();
        await gestor.RegistrarAsync(Memoria(1, 1), "uno", "Creación", 1);
        await gestor.RegistrarAsync(Memoria(1, 2), "dos", "Actualización", 1);

        var v1 = await gestor.ObtenerAsync(1, 1);
        await gestor.RegistrarAsync(Memoria(1, 3), v1!.Contenido, "Reversión a v1.", 1);

        Assert.Equal("uno", v1.Contenido);
        Assert.Equal("uno", (await gestor.ObtenerAsync(1, 3))!.Contenido);
        Assert.Null(await gestor.ObtenerAsync(1, 99));
    }

    [Fact]
    public async Task VersionManager_ListaDtoEnOrdenDeVersion()
    {
        var (gestor, _) = FabricaVersiones();
        await gestor.RegistrarAsync(Memoria(1, 1), "uno", "Creación", 1);
        await gestor.RegistrarAsync(Memoria(1, 2), "dos", "Actualización", 1);

        var dtos = await gestor.ListarDtoAsync(1);

        Assert.Equal(new[] { 1, 2 }, dtos.Select(d => d.Numero));
        Assert.Equal("Actualización", dtos[1].Motivo);
    }
}