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
                new MemoryScope { IdScope = 4, Codigo = "Agente", Nombre = "Agente", Activo = true }
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
        var m = new Mock<IAutorizacionService>();
        m.Setup(a => a.VerificarPermisoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string codigo, CancellationToken _) => new ResultadoAutorizacion
            {
                Permitido = permitidos.Contains(codigo)
            });
        return m;
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
        var retencion = new MemoryRetentionEngine(repo, versiones, logs, busqueda,
            NullLogger<MemoryRetentionEngine>.Instance);
        var manager = new MemoryManager(repo, versiones, logs, busqueda, retencion,
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
}