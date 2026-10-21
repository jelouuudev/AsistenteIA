using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Memoria;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Moq;

namespace Asistente.Tests.Services;

/// <summary>
/// Dobles en memoria del subsistema de memoria (ETAPA 21). Compartidos por los
/// tests de retencion, busqueda semantica, integracion con RAG y flujos
/// funcionales, para que los cuatro STORYBOARD usen el mismo comportamiento de
/// repositorio que el resto de la suite.
/// </summary>
public static class MemoriaTestDoubles
{
    public sealed class RepoMemoria : IMemoryRepository
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
            Categorias.Add(new MemoryCategory { IdCategory = 2, Codigo = "Definicion", Nombre = "Definición", Activo = true });
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

        public HashSet<string> PropietariosInexistentes { get; } = new();

        public Task<bool> PropietarioExisteAsync(string ambito, int idPropietario, CancellationToken ct = default)
            => Task.FromResult(!PropietariosInexistentes.Contains(
                $"{ambito.Trim().ToUpperInvariant()}:{idPropietario}"));
    }

    public sealed class RepoVersiones : IMemoryVersionRepository
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

    public sealed class RepoLogs : IMemoryAccessLogRepository
    {
        public List<MemoryAccessLog> Datos { get; } = new();

        public Task AddAsync(MemoryAccessLog l, CancellationToken ct = default)
        {
            Datos.Add(l);
            return Task.CompletedTask;
        }

        public Task<List<MemoryAccessLog>> GetByMemoryAsync(int id, int tope = 100, CancellationToken ct = default)
            => Task.FromResult(Datos.Where(l => l.IdMemory == id).Take(tope).ToList());

        public Task<List<MemoryAccessLog>> GetRecientesAsync(int tope = 50, CancellationToken ct = default)
            => Task.FromResult(Datos.OrderByDescending(l => l.Fecha).Take(tope).ToList());

        public Task<long> ContarPorOperacionAsync(string op, CancellationToken ct = default)
            => Task.FromResult(Datos.LongCount(l => l.Operacion == op));

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

    /// <summary>
    /// Vector store en memoria que solo devuelve vectores marcados como memoria
    /// (prefijo "MEMORIA:"), igual que el ChromaDB real en este escenario.
    /// </summary>
    public sealed class VectorFalso : IVectorStore
    {
        public Dictionary<Guid, (string Texto, float[] Vec, string Nombre)> Indice { get; } = new();

        public Task IndexAsync(VectorDocument d)
        {
            Indice[d.DocumentId] = (d.Text, d.Embedding, d.MetadataDocumentoNombre ?? string.Empty);
            return Task.CompletedTask;
        }

        public Task IndexBatchAsync(IEnumerable<VectorDocument> ds)
        {
            foreach (var d in ds) Indice[d.DocumentId] = (d.Text, d.Embedding, d.MetadataDocumentoNombre ?? string.Empty);
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
                    MetadataDocumentoNombre = kv.Value.Nombre
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

    /// <summary>Embeddings fijos: 4 dimensiones, siempre el mismo vector.</summary>
    public sealed class EmbeddingsFalsos : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text)
            => Task.FromResult(new float[] { 1f, 0f, 0f, 0f });
    }

    /// <summary>Embeddings que fallan: la búsqueda semántica debe degradar a texto.</summary>
    public sealed class EmbeddingsCaidos : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text)
            => Task.FromResult(Array.Empty<float>());
    }

    /// <summary>Autorización: concede los permisos indicados y siempre MEMORIA_ADMINISTRAR.</summary>
    public static Mock<IAutorizacionService> Auth(params string[] permitidos)
    {
        var concedidos = new HashSet<string>(permitidos) { MemoryManager.PermisoAdministrarMemoria };
        var m = new Mock<IAutorizacionService>();
        m.Setup(a => a.VerificarPermisoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string codigo, CancellationToken _) => new ResultadoAutorizacion
            {
                Permitido = concedidos.Contains(codigo)
            });
        m.Setup(a => a.VerificarAsistenteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });
        return m;
    }

    /// <summary>Ensamblaje del manager con los dobles en memoria.</summary>
    public static (MemoryManager Manager, RepoMemoria Repo, RepoVersiones Versiones,
        RepoLogs Logs, SemanticMemorySearch Busqueda, MemoryRetentionEngine Retencion)
        CrearManager(Mock<IAutorizacionService> autorizacion, VectorFalso? vectores = null)
    {
        var repo = new RepoMemoria();
        var versiones = new RepoVersiones();
        var logs = new RepoLogs();
        var busqueda = new SemanticMemorySearch(
            vectores ?? new VectorFalso(), new EmbeddingsFalsos(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SemanticMemorySearch>.Instance);
        var retencion = new MemoryRetentionEngine(repo, logs, busqueda,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryRetentionEngine>.Instance);
        var manager = new MemoryManager(repo, new MemoryVersionManager(versiones), logs, busqueda, retencion,
            autorizacion.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryManager>.Instance);
        return (manager, repo, versiones, logs, busqueda, retencion);
    }

    /// <summary>Orquestador de memoria sobre los dobles (para los tests de RAG).</summary>
    public static MemoryOrchestrator CrearOrchestrator(MemoryManager manager, RepoMemoria repo)
        => new MemoryOrchestrator(manager, repo, Mock.Of<IMemoriaService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryOrchestrator>.Instance);
}
