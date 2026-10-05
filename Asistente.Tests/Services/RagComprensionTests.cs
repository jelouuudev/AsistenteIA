using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// RAG por comprensión (plan #9069): sin diccionario de secciones, sin listas de
/// keywords, sin recorte por títulos. La consulta va íntegra a embeddings, la
/// preferencia de documento sale del catálogo vivo y el contexto son chunks top
/// por score citados con su documento. Ningún test nombra dominios del código:
/// todo sale de los fakes.
///
public class RagComprensionTests
{
    private sealed class VectorStoreFijo : IVectorStore
    {
        private readonly List<VectorSearchResult> _resultados;
        public string? UltimaConsulta { get; private set; }

        public VectorStoreFijo(List<VectorSearchResult> resultados) => _resultados = resultados;

        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK)
        {
            UltimaConsulta = query;
            return Task.FromResult(_resultados.Take(topK).AsEnumerable());
        }

        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string query, int topK, VectorSearchFilter filter)
            => SearchAsync(query, topK);

        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int documentoProcesadoId)
            => Task.FromResult(Enumerable.Empty<VectorSearchResult>());

        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync()
            => Task.FromResult(Enumerable.Empty<int>());

        public Task<string> GetDocumentTextAsync(int documentoProcesadoId)
            => Task.FromResult(string.Empty);

        public Task IndexAsync(VectorDocument document) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<VectorDocument> documents) => Task.CompletedTask;
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string query, int topK, string documentNameContains)
            => SearchAsync(query, topK);
        public Task DeleteDocumentAsync(Guid documentId) => Task.CompletedTask;
        public Task DeleteByDocumentoProcesadoIdAsync(int documentoProcesadoId) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(0);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task ClearAsync() => Task.CompletedTask;
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync()
            => Task.FromResult(Enumerable.Empty<(string Nombre, int ChunkCount)>());
    }

    private static VectorSearchResult Chunk(int orden, float score, string texto) => new()
    {
        DocumentoProcesadoId = 7,
        ChunkId = orden,
        Text = texto,
        Score = score,
        Orden = orden,
        MetadataDocumentoNombre = "ejemplo",
        MetadataDocumentoCodigo = "ejemplo"
    };

    private static RagService CrearServicio(
        VectorStoreFijo store, List<Documento>? documentos = null, float? puntajeMinimo = null)
    {
        var config = new Mock<IEmbeddingConfiguracionRepository>();
        config.Setup(r => r.GetActivaAsync())
            .ReturnsAsync(puntajeMinimo.HasValue
                ? new EmbeddingConfiguracion { PuntajeMinimo = puntajeMinimo.Value }
                : null);

        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(documentos ?? new List<Documento>());

        return new RagService(
            store,
            new Mock<IEmbeddingProvider>().Object,
            config.Object,
            new Mock<ILogger<RagService>>().Object,
            repo.Object);
    }

    private static List<VectorSearchResult> ChunksTrailer() => new()
    {
        Chunk(12, 0.88f, "Un trailer da la localización de la tabla de referencia cruzada."),
        Chunk(3, 0.85f, "Los objetos de un PDF son booleanos, números, cadenas y nombres."),
        Chunk(9, 0.80f, "La tabla de referencia cruzada contiene información de los objetos indirectos."),
    };

    /// <summary>
    /// La consulta va íntegra al vector store: antes se reemplazaba por el eco
    /// de la pregunta ("trailer de archivo, y dime cuantos...") como supuesta
    /// "sección detectada".
    /// </summary>
    [Fact]
    public async Task Consulta_VaIntegra_AlVectorStore()
    {
        var store = new VectorStoreFijo(ChunksTrailer());
        var svc = CrearServicio(store);
        const string pregunta = "hablame de el trailer de archivo, y dime cuantos productos son de categoria electronica?";

        await svc.RecuperarContextoDocumentalAsync(pregunta, topK: 8);

        Assert.Equal(pregunta, store.UltimaConsulta);
    }

    [Fact]
    public async Task Contexto_OrdenaPorScore_CitaDocumento_SinEtiquetaInventada()
    {
        var store = new VectorStoreFijo(ChunksTrailer());
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync("hablame de el trailer de archivo?", topK: 8);

        Assert.Equal(3, res.TotalFragmentos);
        Assert.Contains("Según **ejemplo**", res.ContextoDocumental);
        Assert.DoesNotContain("Sección detectada:", res.ContextoDocumental);
        Assert.DoesNotContain("INSTRUCCIÓN:", res.ContextoDocumental);
        // El mejor chunk (trailer) va primero: ranking por score, no por orden.
        Assert.True(res.ContextoDocumental.IndexOf("trailer da la localización")
            < res.ContextoDocumental.IndexOf("booleanos"));
    }

    [Fact]
    public async Task Preferencia_SaleDelCatalogoVivo_NoDeNombresFijos()
    {
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            Chunk(1, 0.90f, "Contenido de otro documento."),
            Chunk(2, 0.60f, "Un trailer da la localización de la tabla."),
        });
        var docs = new List<Documento>
        {
            new() { Codigo = "ejemplo", Nombre = "ejemplo", Estado = EstadoDocumento.Activo }
        };
        var svc = CrearServicio(store, docs);

        var res = await svc.RecuperarContextoDocumentalAsync("segun el archivo ejemplo que es el trailer?", topK: 8);

        Assert.Equal(2, res.TotalFragmentos);
        Assert.Contains("Según **ejemplo**", res.ContextoDocumental);
    }

    [Fact]
    public async Task Corte_de_relevancia_Deja_el_ruido_de_otros_documentos()
    {
        // El fragmento que responde puntúa 0.73; el relleno de otro documento 0.54
        // (por encima del mínimo absoluto de la config). Solo debe quedar el bueno.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.73f,
                Text = "Planetas del Sistema Solar y sus distancias.",
                MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
            new()
            {
                DocumentoProcesadoId = 8, ChunkId = 2, Orden = 2, Score = 0.54f,
                Text = "Un trailer da la localizacion de la tabla de referencia cruzada.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync("dime cuales son los Planetas del Sistema Solar", topK: 5);

        Assert.Equal(1, res.TotalFragmentos);
        Assert.DoesNotContain("trailer", res.ContextoDocumental, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Relleno_de_otro_documento_NoSe_Incluye_Aunque_Pase_el_Margen()
    {
        // Caso real del plan #9099: el acierto puntúa 0.669 y el relleno de otro
        // documento 0.555, o sea DENTRO del margen relativo (0.12). Con la sola
        // segunda señal el relleno se colaba; se descarta por ser de otro documento
        // y no alcanzar la relevancia clara (0.60).
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.669f,
                Text = "Distancia Tierra a Marte: 78 millones de km.",
                MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
            new()
            {
                DocumentoProcesadoId = 8, ChunkId = 2, Orden = 2, Score = 0.555f,
                Text = "Un Objeto Diccionario es una tabla asociativa de pares clave-valor.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync("segun astronomia, cual es la Distancia Tierra a Marte?", topK: 5);

        Assert.Equal(1, res.TotalFragmentos);
        Assert.Contains("78 millones", res.ContextoDocumental);
        Assert.DoesNotContain("Diccionario", res.ContextoDocumental);
    }

    [Fact]
    public async Task Acierto_Bajo_El_Suelo_Absoluto_NoSe_Pierde()
    {
        // "distancia entre la tierra y marte" puntúa 0.577 (por debajo de 0.60):
        // es el fragmento ganador de su documento, así que se conserva. Un corte
        // por umbral absoluto lo habría perdido.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.577f,
                Text = "Distancia entre la Tierra y Marte: 78 millones de km.",
                MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
            new()
            {
                DocumentoProcesadoId = 8, ChunkId = 2, Orden = 2, Score = 0.524f,
                Text = "Fragmento genérico de un manual de PDF.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync("distancia entre la tierra y marte", topK: 5);

        Assert.Equal(1, res.TotalFragmentos);
        Assert.Contains("78 millones", res.ContextoDocumental);
    }

    [Fact]
    public async Task Relleno_de_otro_documento_Aunque_pase_de_0_60_NoSe_Incluye()
    {
        // Plan #9121: el acierto (Bono Vacacional) puntúa 0.707 y el fragmento del
        // manual de PDF 0.637. Está dentro del margen relativo (0.12) y supera 0.60,
        // así que solo el umbral de relevancia clara entre documentos lo descarta.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.707f,
                Text = "Bono Vacacional: monto 300 USD por periodo vacacional.",
                MetadataDocumentoNombre = "v", MetadataDocumentoCodigo = "v"
            },
            new()
            {
                DocumentoProcesadoId = 8, ChunkId = 2, Orden = 2, Score = 0.637f,
                Text = "Flujos de contenido PDF y objetos de diccionario.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync("dame informacion sobre el Bono Vacacional", topK: 5);

        Assert.Equal(1, res.TotalFragmentos);
        Assert.Contains("300 USD", res.ContextoDocumental);
        Assert.DoesNotContain("diccionario", res.ContextoDocumental, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fragmento_De_Otro_Documento_Claramente_Relevante_Si_Se_Conserva()
    {
        // Respuesta repartida entre dos documentos: el segundo fragmento es de otra
        // fuente pero claramente relevante (0.75), así que no se descarta.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.81f,
                Text = "El trailer da la localizacion de la tabla de referencia cruzada.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
            new()
            {
                DocumentoProcesadoId = 9, ChunkId = 2, Orden = 2, Score = 0.75f,
                Text = "El trailer del PDF tambien es un objeto indirecto.",
                MetadataDocumentoNombre = "manual", MetadataDocumentoCodigo = "manual"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync("que es el trailer de un pdf", topK: 5);

        Assert.Equal(2, res.TotalFragmentos);
    }

    [Fact]
    public async Task Dos_Documentos_Empatados_Se_Conservan_Los_Dos()
    {
        // Plan #9138: "vacaciones por antigüedad" (v, 0.6699) y "temperatura en
        // Venus" (a, 0.6708) empatan. Antes el segundo se descartaba por ser de
        // otro documento bajo 0.70; ahora el empate lo conserva.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.6708f,
                Text = "Temperatura en Venus: 475 grados Celsius.",
                MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
            new()
            {
                DocumentoProcesadoId = 9, ChunkId = 2, Orden = 2, Score = 0.6699f,
                Text = "Dias de Vacaciones por Antiguedad: 1 a 3 anos 12 dias habiles.",
                MetadataDocumentoNombre = "v", MetadataDocumentoCodigo = "v"
            },
            new()
            {
                DocumentoProcesadoId = 8, ChunkId = 3, Orden = 3, Score = 0.533f,
                Text = "La tabla de referencia cruzada del PDF.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync(
            "hablame de los Dias de Vacaciones por Antiguedad y cual es la Temperatura en Venus", topK: 5);

        Assert.Equal(2, res.TotalFragmentos);
        Assert.Contains("475 grados", res.ContextoDocumental);
        Assert.Contains("12 dias habiles", res.ContextoDocumental);
        Assert.DoesNotContain("referencia cruzada", res.ContextoDocumental);
    }

    [Fact]
    public async Task Preferencia_Por_Codigo_Corto_Protege_Sus_Fragmentos()
    {
        // Los códigos pueden ser de 1 letra ('s') y el match por nombre exige 3+:
        // la preferencia también vale por código exacto del catálogo.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.60f,
                Text = "Separacion de residuos en 4 categorias.",
                MetadataDocumentoNombre = "s", MetadataDocumentoCodigo = "s"
            },
            new()
            {
                DocumentoProcesadoId = 8, ChunkId = 2, Orden = 2, Score = 0.62f,
                Text = "Un trailer da la localizacion de la tabla.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync(
            "segun sotenibilidad dime las iniciativas actuales", topK: 5, savedDocumentPreference: "s");

        Assert.Contains("residuos", res.ContextoDocumental);
        // La preferencia se cumple sin necesidad de BORRAR el resto: el filtro duro
        // era el mecanismo, no la garantia. Lo que decide que el resto se quede o
        // se vaya es el corte de relevancia de mas abajo (test de competencia).
    }

    [Fact]
    public async Task Preferencia_NoExpulsaAlDocumentoQueGanaLaCompetingPregunta()
    {
        // Plan #13189: "hablame sobre el Cuerpo de Archivo segun ejemplo, y sobre la
        // Oferta Laboral segun procedimientos de contratacion". La preferencia es
        // 'ejemplo' (aparece en la primera mitad), pero el chunk de 'r' punteaba
        // 0.7416, el MAYOR de toda la busqueda. Con la preferencia como filtro duro
        // se descartaba y la segunda mitad de la pregunta se quedaba sin respuesta.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.7108f,
                Text = "La tabla de referencia cruzada contiene los objetos indirectos.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
            new()
            {
                DocumentoProcesadoId = 9, ChunkId = 2, Orden = 2, Score = 0.7416f,
                Text = "Procedimientos de Contratacion y Oferta Laboral.",
                MetadataDocumentoNombre = "r", MetadataDocumentoCodigo = "r"
            },
        });
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync(
            "hablame sobre el Cuerpo de Archivo segun ejemplo, y sobre la Oferta Laboral",
            topK: 5, savedDocumentPreference: "ejemplo");

        // Ambos documentos quedan representados: la segunda mitad de la pregunta
        // ("Oferta Laboral") ya no se ignora. El orden final lo fija el corte de
        // relevancia (aquí manda el fragmento mas puntudo, que es el de 'r'), no la
        // preferencia, asi que no se afirma un orden concreto.
        Assert.Contains("objetos indirectos", res.ContextoDocumental);
        Assert.Contains("Oferta Laboral", res.ContextoDocumental);
    }

    [Fact]
    public async Task DocumentoCompetidor_SoloEntraSiSuperaElCorteDeRelevancia()
    {
        // La contracara: ser de otro documento no basta para colarse. Con corte 0.70
        // el fragmento de 'ejemplo' (0.62) se queda fuera y el de 'r' (0.7416)
        // entra, porque el filtro es de puntaje, no de pertenencia al preferido.
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.62f,
                Text = "Un trailer da la localizacion de la tabla.",
                MetadataDocumentoNombre = "ejemplo", MetadataDocumentoCodigo = "ejemplo"
            },
            new()
            {
                DocumentoProcesadoId = 9, ChunkId = 2, Orden = 2, Score = 0.7416f,
                Text = "Procedimientos de Contratacion y Oferta Laboral.",
                MetadataDocumentoNombre = "r", MetadataDocumentoCodigo = "r"
            },
        });
        var svc = CrearServicio(store, puntajeMinimo: 0.70f);

        var res = await svc.RecuperarContextoDocumentalAsync(
            "oferta laboral y procedimientos de contratacion",
            topK: 5, savedDocumentPreference: "r");

        Assert.Contains("Oferta Laboral", res.ContextoDocumental);
        Assert.DoesNotContain("trailer", res.ContextoDocumental, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DividirFrases_NoParte_Versiones_Ni_Numerales()
    {
        var frases = RagService.DividirFrases("La cabecera debería ser %PDF-1.7 de prueba. Siguiente frase aquí.");

        Assert.Equal(2, frases.Count);
        Assert.Contains("1.7", frases[0]);
    }

    [Fact]
    public void DividirFrases_Parte_Por_Punto_Y_Salto()
    {
        // El encabezado viaja con su primera línea (no se tritura) y el
        // siguiente encabezado abre otro bloque.
        var frases = RagService.DividirFrases(
            "1. Cabecera de Fichero\nLa primera línea identifica la versión PDF.\n2. Cuerpo de Archivo");

        Assert.Equal(2, frases.Count);
        Assert.Contains("primera línea", frases[0]);
        Assert.Equal("2. Cuerpo de Archivo", frases[1]);
    }

    [Fact]
    public void DividirFrases_Desajusta_Saltos_A_Mitad_De_Frase()
    {
        // Salto de la extracción PDF a mitad de frase: se une, no se parte.
        var frases = RagService.DividirFrases(
            "para permitir un acceso eficiente en un\nentorno de red. Siguiente frase aquí.");

        Assert.Equal(2, frases.Count);
        Assert.Contains("en un entorno de red.", frases[0]);
    }

    [Fact]
    public void DividirFrases_Texto_De_Una_Sola_Frase_No_Se_Recorta()
    {
        Assert.Empty(RagService.DividirFrases("Un solo enunciado sin más cortes"));
    }

    /// <summary>
    /// Embeddings fijos por marca textual para el recorte por frases (plan #10181):
    /// SEMILLA responde (0.90), MEDIA parcial (0.65), CERCANA acompaña (0.58),
    /// el resto es ruido (0.50).
    /// </summary>
    private sealed class EmbFrases : IEmbeddingProvider
    {
        private static readonly float[] Pregunta = { 1f, 0f };
        private static readonly float[] Semilla = { 0.9f, 0.436f };
        private static readonly float[] Media = { 0.65f, 0.76f };
        private static readonly float[] Cercana = { 0.58f, 0.815f };
        private static readonly float[] Ruido = { 0.5f, 0.866f };
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (text.Contains("SEMILLA")) return Task.FromResult(Semilla);
            if (text.Contains("MEDIA")) return Task.FromResult(Media);
            if (text.Contains("CERCANA")) return Task.FromResult(Cercana);
            if (text.Contains("pregunta de prueba")) return Task.FromResult(Pregunta);
            return Task.FromResult(Ruido);
        }
    }

    private static RagService CrearServicioConEmbeddings(VectorStoreFijo store, IEmbeddingProvider emb)
    {
        var config = new Mock<IEmbeddingConfiguracionRepository>();
        config.Setup(r => r.GetActivaAsync()).ReturnsAsync((EmbeddingConfiguracion?)null);
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Documento>());
        return new RagService(store, emb, config.Object,
            new Mock<ILogger<RagService>>().Object, repo.Object);
    }

    private static string Relleno(string tema, int veces)
        => string.Join(" ", Enumerable.Repeat($"Frase de relleno sobre {tema} sin relación con la consulta formulada.", veces));

    [Fact]
    public async Task RecortePorFrases_Conserva_Semilla_Y_Corrida_Descarta_Ruido()
    {
// Chunks separados: la respuesta en su propio pasaje y el ruido en otro.
        // Antes iban en el mismo fragmento y el relleno "- clima", "- deportes"
        // tenía que desaparecer por el recorte por frases; con el chunk completo
        // para coincidencias fuertes (#13215) eso ya no puede ser así, porque un
        // chunk es un pasaje continuo. El ruido que no responde vive en su chunk.
        var respuesta = " SEMILLA primera frase que responde directamente a la pregunta de prueba formulada aqu�."
            + " CERCANA continuaci�n con contexto relevante para la pregunta de prueba realizada.";
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.75f,
                Text = respuesta, MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 2, Orden = 2, Score = 0.64f,
                Text = "9. Otra seccion sin relacion con la pregunta.\n" + Relleno("clima", 30) + " " + Relleno("deportes", 30),
                MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
        });
        var svc = CrearServicioConEmbeddings(store, new EmbFrases());

        var res = await svc.RecuperarContextoDocumentalAsync("pregunta de prueba sobre el tema", topK: 5);

        Assert.Contains("SEMILLA", res.ContextoDocumental);
        Assert.Contains("CERCANA", res.ContextoDocumental);
        Assert.DoesNotContain("clima", res.ContextoDocumental);
        Assert.DoesNotContain("deportes", res.ContextoDocumental);
    }

    [Fact]
    public async Task RecortePorFrases_SinSemillas_Muestra_Mejor_Y_Vecinas()
    {
        // Fragmento largo sin nada que alcance 0.60: antes pasaba entero (900+
        // caracteres). Ahora se toma la mejor frase con sus vecinas y la
        // continuación exige suelo de corrida (0.55): el relleno (0.50) no
        // entra, y el encabezado ("7. Seccion siguiente…") tampoco.
        var texto = Relleno("norte", 10) + " " + Relleno("sur", 10) + " 7. Seccion siguiente que no responde.";
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.70f,
                Text = texto, MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
        });
        var svc = CrearServicioConEmbeddings(store, new EmbFrases());

        var res = await svc.RecuperarContextoDocumentalAsync("pregunta de prueba sobre el tema", topK: 5);

        Assert.Equal(1, res.TotalFragmentos);
        Assert.Contains("norte", res.ContextoDocumental);
        // El relleno por debajo del suelo no entra aunque venga después, y el
        // encabezado marca otra sección: ambos fuera.
        Assert.DoesNotContain("sur", res.ContextoDocumental, StringComparison.Ordinal);
        Assert.DoesNotContain("Seccion siguiente", res.ContextoDocumental, StringComparison.Ordinal);
        Assert.True(res.ContextoDocumental.Length < 900,
            "no debe volver el fragmento entero: " + res.ContextoDocumental.Length);
    }

    [Fact]
    public async Task Segunda_Corrida_Debil_Del_Mismo_Documento_Se_Descarta()
    {
// Un fragmento con dos corridas con semilla: la mejor (0.90) se conserva;
        // la segunda (0.65) no alcanza relevancia clara y se descarta aunque pase
        // el suelo. Por documento, solo la mejor corrida salvo >= 0.70.
        var texto = "SEMILLA primera frase que responde directamente a la pregunta de prueba formulada aqu�. "
            + Relleno("medio", 10) + " "
            + "MEDIA segunda respuesta parcial a la pregunta de prueba realizada. "
            + Relleno("sur", 10) + " 7. Seccion siguiente que no responde.";
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.70f,
                Text = texto, MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
        });
        var svc = CrearServicioConEmbeddings(store, new EmbFrases());

        var res = await svc.RecuperarContextoDocumentalAsync("pregunta de prueba sobre el tema", topK: 5);

        Assert.Contains("primera frase", res.ContextoDocumental);
        // Lo que sigue dentro de la MISMA seccion si se entrega (continuidad de
        // seccion, #13219), pero no como un passage propio: una sola corrida.
        Assert.Equal(1, res.FragmentosRecuperados.Count(x => x.DocumentoNombre == "a"));
    }

    [Fact]
    public async Task Segunda_Corrida_Fuerte_Del_Mismo_Documento_Se_Conserva()
    {
        // Dos corridas >= 0.70 en el mismo documento: ambas responden.
        var texto = "SEMILLA primera frase que responde directamente a la pregunta de prueba formulada aquí. "
            + Relleno("medio", 10) + " "
            + "SEMILLA segunda frase que también responde a la pregunta de prueba formulada. "
            + Relleno("sur", 10);
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.70f,
                Text = texto, MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
        });
        var svc = CrearServicioConEmbeddings(store, new EmbFrases());

        var res = await svc.RecuperarContextoDocumentalAsync("pregunta de prueba sobre el tema", topK: 5);

        Assert.Contains("primera frase", res.ContextoDocumental);
        Assert.Contains("segunda frase", res.ContextoDocumental);
    }

    [Fact]
    public void NormalizarFrase_Iguala_Variantes_De_Espaciado()
    {
        Assert.Equal(
            RagService.NormalizarFrase("A partir de la especificación 1.4, la versión."),
            RagService.NormalizarFrase("A partir de la especificación1.4 la versión!"));
    }

    [Fact]
    public async Task Frases_Duplicadas_Literales_Aparecen_Una_Sola_Vez()
    {
        // El PDF trae párrafos repetidos literal (plan #10182): el duplicado se
        // descarta por igualdad exacta, sin umbrales.
        var repetida = "SEMILLA frase repetida literal en dos lugares para la pregunta de prueba formulada.";
        var texto = repetida + " " + Relleno("medio", 14) + " " + repetida + " " + Relleno("sur", 8);
        var store = new VectorStoreFijo(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.70f,
                Text = texto, MetadataDocumentoNombre = "a", MetadataDocumentoCodigo = "a"
            },
        });
        var svc = CrearServicioConEmbeddings(store, new EmbFrases());

        var res = await svc.RecuperarContextoDocumentalAsync("pregunta de prueba sobre el tema", topK: 5);

        var ocurrencias = res.ContextoDocumental.Split("frase repetida literal").Length - 1;
        Assert.Equal(1, ocurrencias);
    }

    [Fact]
    public async Task SinResultados_ContextoVacio()
    {
        var store = new VectorStoreFijo(new List<VectorSearchResult>());
        var svc = CrearServicio(store);

        var res = await svc.RecuperarContextoDocumentalAsync("pregunta sin respuesta?", topK: 8);

        Assert.Equal(0, res.TotalFragmentos);
        Assert.True(string.IsNullOrWhiteSpace(res.ContextoDocumental));
    }

    [Fact]
    public void TruncarPorFrase_TerminaEnFraseCompleta()
    {
        var texto = "Primera frase completa. Segunda frase completa que es más larga y sigue. Tercera.";
        var recorte = RagService.TruncarPorFrase(texto, 60);

        Assert.Equal("Primera frase completa.", recorte);
    }

    [Fact]
    public void TruncarPorFrase_SinFraseCompleta_CorteDuro()
    {
        var texto = "una sola frase larguísima sin puntos intermedios de ningún tipo imaginable";
        var recorte = RagService.TruncarPorFrase(texto, 30);

        Assert.Equal(texto[..30].Trim(), recorte);
    }
}

/// <summary>
/// Plan #13191: "Cuerpo de Archivo segun ejemplo, y Oferta Laboral segun
/// procedimientos de contratacion". El documento 'r' entra (ya no lo expulsa la
/// preferencia) pero se quedaba con su unica mejor corrida, que era solo el
/// titulo; sus vinetas (duracion, entrevistas, validez de la oferta) quedaban en
/// corridas por debajo del 0.70. El suelo de presupuesto es solo para documentos
/// SECUNDARIOS: el principal mantiene el recorte de una sola corrida (#10181).
/// </summary>
public class RagPresupuestoPorDocumentoTests
{
    private sealed class Store : IVectorStore
    {
        private readonly List<VectorSearchResult> _r;
        public Store(List<VectorSearchResult> r) => _r = r;
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string q, int topK)
            => Task.FromResult(_r.Take(topK).AsEnumerable());
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int topK, VectorSearchFilter f) => SearchAsync(q, topK);
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id) => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync() => Task.FromResult(Enumerable.Empty<int>());
        public Task<string> GetDocumentTextAsync(int id) => Task.FromResult(string.Empty);
        public Task IndexAsync(VectorDocument d) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<VectorDocument> d) => Task.CompletedTask;
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int topK, string n) => SearchAsync(q, topK);
        public Task DeleteDocumentAsync(Guid id) => Task.CompletedTask;
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(0);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task ClearAsync() => Task.CompletedTask;
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync() => Task.FromResult(Enumerable.Empty<(string, int)>());
    }

    private sealed class Emb : IEmbeddingProvider
    {
        // Vectores en 2D con la pregunta en [1,0], de modo que el coseno con la
        // pregunta es exactamente la primera coordenada del vector de la frase.
        private readonly Dictionary<string, float[]> _porTexto = new();
        public string? Consulta { get; set; }
        public void learned(string texto, double coseno)
            => _porTexto[texto] = new[] { (float)coseno, (float)Math.Sqrt(Math.Max(0, 1 - coseno * coseno)) };
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (Consulta != null && text.Contains(Consulta, StringComparison.Ordinal))
                return Task.FromResult(new[] { 1f, 0f });
            foreach (var kv in _porTexto)
                if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(kv.Value);
            return Task.FromResult(new[] { 0.45f, 0.893f });
        }
    }

    private static VectorSearchResult Chunk(int doc, int orden, float score, string texto) => new()
    {
        DocumentoProcesadoId = doc,
        ChunkId = orden,
        Orden = orden,
        Score = score,
        Text = texto,
        MetadataDocumentoNombre = doc == 7 ? "ejemplo" : "r",
        MetadataDocumentoCodigo = doc == 7 ? "ejemplo" : "r"
    };

    private static RagService Crear(Store store, Emb emb)
    {
        var config = new Mock<IEmbeddingConfiguracionRepository>();
        config.Setup(r => r.GetActivaAsync()).ReturnsAsync((EmbeddingConfiguracion?)null);
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Documento>());
        return new RagService(store, emb, config.Object,
            new Mock<ILogger<RagService>>().Object, repo.Object);
    }

        private static string RellenoMarginal(int lineas) => string.Join("\n",
        Enumerable.Range(0, lineas).Select(i => $"Relleno marginal {i} sin contenido util."));

    private static string Relleno(int lineas) => string.Join("\n",
        Enumerable.Range(0, lineas).Select(i => $"Relleno variante {i} sin contenido util."));

    // Cada frase lleva un token unico: el mock resuelve por "primer token que
    // coincide", asi que dos frases nunca pueden solaparse.
    private static Emb PrepararEmb(string consulta)
    {
        var emb = new Emb { Consulta = consulta };
        emb.learned("Relleno variante", 0.42);
        emb.learned("Separador Alfa", 0.40);
        emb.learned("Separador Beta", 0.40);
        emb.learned("Separador Gamma", 0.40);
        emb.learned("Cuerpo del archivo", 0.95);
        emb.learned("Objetos indirectos", 0.92);
        emb.learned("Tabla de referencia cruzada", 0.91);
        emb.learned("Proceso de contratacion", 0.88);
        emb.learned("Duracion maxima del proceso", 0.56);
        emb.learned("Entrevistas del proceso", 0.56);
        emb.learned("Validez de la oferta", 0.57);
        emb.learned("Documentacion requerida", 0.57);
        emb.learned("Trailer del archivo", 0.56);
        return emb;
    }

    [Fact]
    public async Task DocumentoSecundario_RecibeSueloDePresupuesto()
    {
        var consulta = "hablame sobre el cuerpo del archivo y sobre la oferta laboral segun proceso de contratacion";
        var emb = PrepararEmb(consulta);
        var store = new Store(new List<VectorSearchResult>
        {
            Chunk(7, 1, 0.74f, "Cuerpo del archivo.\nObjetos indirectos.\nTabla de referencia cruzada.\n"
                + "Trailer del archivo.\n7. Seccion siguiente que no responde.\n" + Relleno(40)),
            // El titulo de 'r' encaja con la pregunta (0.88) pero sus datos no
            // (0.56-0.57): quedan en frases sueltas, sin semilla que las arrastre.
            Chunk(9, 2, 0.72f, "Proceso de contratacion.\nSeparador Alfa.\nDuracion maxima del proceso.\n"
                + "Separador Beta.\nEntrevistas del proceso.\nSeparador Gamma.\nValidez de la oferta.\n"
                + "Documentacion requerida.\nNota final del pasaje.\nFrase de cierre del pasaje.\n7. Seccion siguiente que no responde."),
            // Chunk MARGINAL: ninguna frase suya debe entrar.
            Chunk(9, 3, 0.60f, RellenoMarginal(40)),
        });

        var res = await Crear(store, emb).RecuperarContextoDocumentalAsync(
            consulta, topK: 5, savedDocumentPreference: "ejemplo");

        Assert.Contains("Cuerpo del archivo", res.ContextoDocumental);
        // El documento secundario ya no llega solo con su titulo.
        Assert.Contains("Duracion maxima del proceso", res.ContextoDocumental);
        Assert.Contains("Validez de la oferta", res.ContextoDocumental);
        // Y el chunk MARGINAL (sin coincidencia fuerte) no entra: ese sigue siendo
        // el filtro que evita que una sección poco relacionada se cuele.
        Assert.DoesNotContain("Relleno variante 31", res.ContextoDocumental, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocumentoUnico_NoRecibeSueloYConservaUnaSolaCorrida()
    {
        // Contracara: con un unico documento el presupuesto NO compra la segunda
        // corrida aunque su frase supere el suelo de corrida (plan #10181).
        var consulta = "cual es el cuerpo del archivo";
        var emb = PrepararEmb(consulta);
        var store = new Store(new List<VectorSearchResult>
        {
            Chunk(7, 1, 0.74f, "Cuerpo del archivo.\nObjetos indirectos.\nTabla de referencia cruzada.\n"
                + "Trailer del archivo.\n7. Seccion siguiente que no responde.\n" + Relleno(40)),
        });

        var res = await Crear(store, emb).RecuperarContextoDocumentalAsync(
            consulta, topK: 5, savedDocumentPreference: "ejemplo");

        Assert.Contains("Objetos indirectos", res.ContextoDocumental);
        // Continuidad de seccion (#13219): lo que sigue a la frase que respondio
        // dentro de la misma seccion se entrega y se para en el encabezado.
        Assert.Contains("Trailer del archivo", res.ContextoDocumental, StringComparison.Ordinal);
        Assert.DoesNotContain("Seccion siguiente", res.ContextoDocumental, StringComparison.Ordinal);
    }
}
/// <summary>
/// Plan #13193: la entregaalternaba documento (ejemplo, r, ejemplo) y salian
/// TRES encabezados "Segun **...**" para dos fuentes; ademas el segundo bloque de
/// 'ejemplo' repetia de forma semantica lo que ya habia dicho el primero.
/// </summary>
public class RagAgrupadoPorDocumentoTests
{
    private sealed class Store : IVectorStore
    {
        private readonly List<VectorSearchResult> _r;
        public Store(List<VectorSearchResult> r) => _r = r;
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string q, int topK)
            => Task.FromResult(_r.Take(topK).AsEnumerable());
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int topK, VectorSearchFilter f) => SearchAsync(q, topK);
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id) => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync() => Task.FromResult(Enumerable.Empty<int>());
        public Task<string> GetDocumentTextAsync(int id) => Task.FromResult(string.Empty);
        public Task IndexAsync(VectorDocument d) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<VectorDocument> d) => Task.CompletedTask;
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int topK, string n) => SearchAsync(q, topK);
        public Task DeleteDocumentAsync(Guid id) => Task.CompletedTask;
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(0);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task ClearAsync() => Task.CompletedTask;
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync() => Task.FromResult(Enumerable.Empty<(string, int)>());
    }

    /// <summary>Dos frases distintas pero semanticamente iguales (0.97) y una
    /// distinta (0.55). Sin esto el dedup exacto no las caza.</summary>
    private sealed class Emb : IEmbeddingProvider
    {
        private readonly Dictionary<string, float[]> _porTexto = new();
        public string? Consulta { get; set; }
        public void learned(string texto, double coseno)
            => _porTexto[texto] = new[] { (float)coseno, (float)Math.Sqrt(Math.Max(0, 1 - coseno * coseno)) };
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (Consulta != null && text.Contains(Consulta, StringComparison.Ordinal))
                return Task.FromResult(new[] { 1f, 0f });
            foreach (var kv in _porTexto)
                if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(kv.Value);
            return Task.FromResult(new[] { 0.45f, 0.893f });
        }
    }

    private static VectorSearchResult Chunk(int doc, int orden, float score, string texto) => new()
    {
        DocumentoProcesadoId = doc, ChunkId = orden, Orden = orden, Score = score, Text = texto,
        MetadataDocumentoNombre = doc == 7 ? "ejemplo" : "r",
        MetadataDocumentoCodigo = doc == 7 ? "ejemplo" : "r"
    };

    private static RagService Crear(Store store, Emb emb)
    {
        var config = new Mock<IEmbeddingConfiguracionRepository>();
        config.Setup(r => r.GetActivaAsync()).ReturnsAsync((EmbeddingConfiguracion?)null);
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Documento>());
        return new RagService(store, emb, config.Object,
            new Mock<ILogger<RagService>>().Object, repo.Object);
    }

    // Cada frase contiene UN solo token registrado ("Contenido repetido" /
    // "Frase distinta"): si compartieran texto, el mock devolvería el vector de
    // la clave común a las dos y el dedup no podría distinguirlas.
    private const string FraseRepetida =
        "Contenido repetido sobre el acceso aleatorio a los objetos del archivo.";
    private const string FraseDistinta =
        "Frase distinta sobre la unica parte que tiene un formato fijo.";

    [Fact]
    public async Task DocumentoAparece_Una_SolaVez_AunqueSusFragmentosAlternen()
    {
        var consulta = "dime sobre el cuerpo del archivo y la oferta laboral";
        var emb = new Emb { Consulta = consulta };
        emb.learned("Relleno variante", 0.42);
        emb.learned("Cuerpo del archivo", 0.93);
        emb.learned("Objetos indirectos", 0.91);
        emb.learned("Tabla de referencia cruzada", 0.90);
        emb.learned("Contenido repetido", 0.97);
        emb.learned("Frase distinta", 0.72);
        emb.learned(FraseRepetida, 0.97);
        emb.learned(FraseDistinta, 0.60);
        emb.learned("Proceso de contratacion", 0.88);
        emb.learned("Duracion maxima del proceso", 0.57);

        var relleno = string.Join("\n", Enumerable.Range(0, 40).Select(i => $"Relleno variante {i} sin contenido util."));
        var store = new Store(new List<VectorSearchResult>
        {
            // Dos fragmentos del MISMO documento, separados por uno de 'r': el
            // orden por puntaje los hace alternar (0.75, 0.72, 0.74).
            Chunk(7, 1, 0.75f, "Cuerpo del archivo.\nObjetos indirectos.\nTabla de referencia cruzada.\n"
                + FraseRepetida + "\n" + relleno),
            Chunk(9, 2, 0.72f, "Proceso de contratacion.\nDuracion maxima del proceso.\n" + relleno),
            Chunk(7, 3, 0.74f, "Tabla de referencia cruzada.\n" + FraseDistinta + "\n" + relleno),
        });

        var res = await Crear(store, emb).RecuperarContextoDocumentalAsync(
            consulta, topK: 5, savedDocumentPreference: "ejemplo");

        // Dos documentos -> DOS encabezados, no tres.
        // Dos documentos -> DOS encabezados, no tres.
        Assert.Equal(1, CuentaOcurrencias(res.ContextoDocumental!, "Seg" + (char)0xFA + "n **ejemplo**"));
        Assert.Equal(1, CuentaOcurrencias(res.ContextoDocumental!, "Seg" + (char)0xFA + "n **r**"));
        // La frase que NO es repetida sobrevive.
        Assert.Contains(FraseDistinta, res.ContextoDocumental);
    }

    private static int CuentaOcurrencias(string texto, string sub) =>
        texto.Split(new[] { sub }, StringSplitOptions.None).Length - 1;
}

/// <summary>
/// Plan #13193, testeado sobre el metodo que decide el agrupado. Con fragmentos
/// que ALTERNAN de documento (ejemplo, r, ejemplo) se emitian tres encabezados
/// "Segun **...**" para dos fuentes y el mismo manual quedaba partido en dos
/// bloques lejanos. El agrupado es por etiqueta, no por contenido.
/// </summary>
public class RagConstruirContextoTests
{
    private static FragmentoRelevanteDto Frag(string doc, int chunk, float score, string texto) => new()
    {
        DocumentoNombre = doc, DocumentoCodigo = doc, ChunkId = chunk, SearchRank = chunk,
        PuntajeSimilitud = score, Texto = texto
    };

    [Fact]
    public void FragmentosAlternados_EmitenUnSoloEncabezadoPorDocumento()
    {
        var fragmentos = new List<FragmentoRelevanteDto>
        {
            Frag("ejemplo", 1, 0.75f, "Cuerpo del archivo."),
            Frag("r", 2, 0.72f, "Procedimientos de contratacion."),
            Frag("ejemplo", 3, 0.74f, "Objetos indirectos del archivo."),
        };

        var ctx = RagService.ConstruirContextoDocumental(fragmentos, 4000);

        Assert.Equal(1, Cuenta(ctx, "Seg" + (char)0xFA + "n **ejemplo**"));
        Assert.Equal(1, Cuenta(ctx, "Seg" + (char)0xFA + "n **r**"));
        // Los dos fragmentos de 'ejemplo' quedan juntos, en un solo bloque.
        Assert.True(ctx.IndexOf("Cuerpo del archivo", StringComparison.Ordinal)
            < ctx.IndexOf("Objetos indirectos", StringComparison.Ordinal));
        // Y solo hay un separador entre los dos documentos.
        Assert.Equal(1, Cuenta(ctx, "\n\n---\n\n"));
    }

    [Fact]
    public void RespetaElLimiteDeCaracteresYLoDice()
    {
        var fragmentos = new List<FragmentoRelevanteDto>
        {
            Frag("ejemplo", 1, 0.75f, new string('a', 400) + ". " + new string('b', 400) + "."),
        };

        var ctx = RagService.ConstruirContextoDocumental(fragmentos, 300);

        Assert.Contains("[... truncado ...]", ctx);
        Assert.True(ctx.Length < 700, "no debe pasarse del limite");
    }

    private static int Cuenta(string texto, string sub) =>
        texto.Split(new[] { sub }, StringSplitOptions.None).Length - 1;
}
/// <summary>
/// Plan #13195: 'r' pasaba el corte de relevancia (0.7096) pero NO aparecia nunca.
/// Motivo: el limite de longitud se lo comia el primer documento y el metodo
/// hacia return. El reparto es ahora justo entre las fuentes que quedan.
/// </summary>
public class RagRepartoDePresupuestoTests
{
    private static FragmentoRelevanteDto Frag(string doc, int chunk, float score, string texto) => new()
    {
        DocumentoNombre = doc, DocumentoCodigo = doc, ChunkId = chunk, SearchRank = chunk,
        PuntajeSimilitud = score, Texto = texto
    };

    private static int Cuenta(string texto, string sub) =>
        texto.Split(new[] { sub }, StringSplitOptions.None).Length - 1;

    [Fact]
    public void DocumentoPrincipal_NoSeComeElPresupuestoDeLosDemas()
    {
        var primero = string.Join(" ", Enumerable.Repeat("contenido del documento principal.", 90));
        var segundo = "Procedimientos de contratacion y oferta laboral.";
        var fragmentos = new List<FragmentoRelevanteDto>
        {
            Frag("ejemplo", 1, 0.75f, primero),
            Frag("r", 2, 0.70f, segundo),
        };

        var ctx = RagService.ConstruirContextoDocumental(fragmentos, 2500);

        Assert.Contains("Seg" + (char)0xFA + "n **ejemplo**", ctx);
        Assert.Contains("Seg" + (char)0xFA + "n **r**", ctx);
        Assert.Contains(segundo, ctx);
        Assert.True(ctx.Length <= 2600, "no debe exceder el limite");
    }

    [Fact]
    public void SiNoCabenTodos_ReparteSinPrometerMasDeLoQueQueda()
    {
        var grande = string.Join(" ", Enumerable.Repeat("bloque largo de contenido.", 200));
        var fragmentos = new List<FragmentoRelevanteDto>
        {
            Frag("a", 1, 0.80f, grande),
            Frag("b", 2, 0.75f, grande),
            Frag("c", 3, 0.70f, grande),
        };

        var ctx = RagService.ConstruirContextoDocumental(fragmentos, 900);

        Assert.True(ctx.Length <= 1000, "el limite manda");
        // Al menos el primero entra completo; no se promete nada del resto.
        Assert.Contains("Seg" + (char)0xFA + "n **a**", ctx);
    }
}
/// <summary>
/// Plan #13218: "dime los pedidos que repartio marco ruiz, y hablame sobre el
/// Trailer de Archivo". El vector UNICO de la pregunta es mitad SQL, mitad PDF y
/// las frases de la segunda mitad caian bajo el suelo: la seccion se cortaba tras
/// su primera frase. Ahora cada frase se puntua contra la mejor CLAUSULA.
/// </summary>
public class RagPuntuacionPorClausulaTests
{
    private sealed class Store : IVectorStore
    {
        private readonly List<VectorSearchResult> _r;
        public Store(List<VectorSearchResult> r) => _r = r;
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string q, int topK)
            => Task.FromResult(_r.Take(topK).AsEnumerable());
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int topK, VectorSearchFilter f) => SearchAsync(q, topK);
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id) => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync() => Task.FromResult(Enumerable.Empty<int>());
        public Task<string> GetDocumentTextAsync(int id) => Task.FromResult(string.Empty);
        public Task IndexAsync(VectorDocument d) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<VectorDocument> d) => Task.CompletedTask;
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int topK, string n) => SearchAsync(q, topK);
        public Task DeleteDocumentAsync(Guid id) => Task.CompletedTask;
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(0);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task ClearAsync() => Task.CompletedTask;
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync() => Task.FromResult(Enumerable.Empty<(string, int)>());
    }

    /// <summary>Vectores en 2D: el coseno con la consulta es la primera
    /// coordenada de la frase. Cada texto tiene su dirección propia.</summary>
    private sealed class Emb : IEmbeddingProvider
    {
        private readonly Dictionary<string, double> _porTexto = new(StringComparer.OrdinalIgnoreCase);
        public void learned(string texto, double coseno) => _porTexto[texto] = coseno;
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            foreach (var kv in _porTexto)
                if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    var c = kv.Value;
                    return Task.FromResult(new[] { (float)c, (float)Math.Sqrt(Math.Max(0, 1 - c * c)) });
                }
            // Mezcla neutra a 45 grados: representa "la pregunta entera".
            return Task.FromResult(new[] { 0.7071f, 0.7071f });
        }
    }

    [Fact]
    public void Clausulas_SeparanPorPuntuacionY_NuncaVacias()
    {
        var clausulas = RagService.DividirEnClausulas(
            "dime los pedidos que repartio marco ruiz, y hablame sobre el Trailer de Archivo").ToList();

        Assert.Equal(2, clausulas.Count);
        Assert.Contains("repartio", clausulas[0]);
        Assert.Contains("Trailer", clausulas[1]);
        Assert.Single(RagService.DividirEnClausulas("una sola parte sin puntuacion"));
    }

    [Fact]
    public async Task FrasesDeLaSegundaParte_NoSePierdenPorDilucion()
    {
        var emb = new Emb();
        // La consulta completa (45 grados) queda a 0.55 de estas frases: es el
        // caso real, "mitad SQL, mitad PDF".
        emb.learned("Consulta completa", 0.55);
        // ...pero la clausula del Trailer las ve claramente.
        emb.learned("hablame sobre el Trailer", 0.85);
        emb.learned("dime los pedidos", 0.30);
        var relleno = string.Join("\n", Enumerable.Range(0, 30).Select(i => $"Linea neutra numero {i} sin relacion."));

        var store = new Store(new List<VectorSearchResult>
        {
            new()
            {
                DocumentoProcesadoId = 7, ChunkId = 1, Orden = 1, Score = 0.74f,
                Text = "Titulo de la seccion.\nPrimera frase que explica el Trailer en detalle.\n"
                     + "Frase que nombra startxref y el offset de bytes.\nNota de cierre del pasaje.\n" + relleno,
                MetadataDocumentoNombre = "manual", MetadataDocumentoCodigo = "manual"
            },
        });

        var config = new Mock<IEmbeddingConfiguracionRepository>();
        config.Setup(r => r.GetActivaAsync()).ReturnsAsync((EmbeddingConfiguracion?)null);
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Documento>());
        var svc = new RagService(store, emb, config.Object,
            new Mock<ILogger<RagService>>().Object, repo.Object);

        var res = await svc.RecuperarContextoDocumentalAsync(
            "dime los pedidos que repartio marco ruiz, y hablame sobre el Trailer de Archivo",
            topK: 5, savedDocumentPreference: "manual");

        Assert.Contains("Trailer", res.ContextoDocumental);
        Assert.Contains("startxref", res.ContextoDocumental);
    }
}
/// <summary>
/// Resumen genérico ("resúmeme el documento"): los puntajes salen PLANOS porque
/// ninguna parte destaca, y optimizar "lo más parecido" entregaba veinte frases
/// de una sola sección. En modo cobertura se toma la mejor corrida DE CADA
/// fragmento, en orden de documento. Sin vocabulario: solo la dispersión.
/// </summary>
public class RagModoCoberturaTests
{
    private sealed class Store : IVectorStore
    {
        private readonly List<VectorSearchResult> _r;
        public Store(List<VectorSearchResult> r) => _r = r;
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string q, int topK)
            => Task.FromResult(_r.Take(topK).AsEnumerable());
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int topK, VectorSearchFilter f) => SearchAsync(q, topK);
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id) => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync() => Task.FromResult(Enumerable.Empty<int>());
        public Task<string> GetDocumentTextAsync(int id) => Task.FromResult(string.Empty);
        public Task IndexAsync(VectorDocument d) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<VectorDocument> d) => Task.CompletedTask;
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int topK, string n) => SearchAsync(q, topK);
        public Task DeleteDocumentAsync(Guid id) => Task.CompletedTask;
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(0);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task ClearAsync() => Task.CompletedTask;
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync() => Task.FromResult(Enumerable.Empty<(string, int)>());
    }

    private sealed class Emb : IEmbeddingProvider
    {
        private readonly Dictionary<string, double> _porTexto = new(StringComparer.OrdinalIgnoreCase);
        public string? Consulta { get; set; }
        public void learned(string texto, double coseno) => _porTexto[texto] = coseno;
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (Consulta != null && text.Contains(Consulta, StringComparison.Ordinal))
                return Task.FromResult(new[] { 1f, 0f });
            foreach (var kv in _porTexto)
                if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    var c = kv.Value;
                    return Task.FromResult(new[] { (float)c, (float)Math.Sqrt(Math.Max(0, 1 - c * c)) });
                }
            return Task.FromResult(new[] { 0.45f, 0.893f });
        }
    }

    private static VectorSearchResult Chunk(int orden, float score, string texto) => new()
    {
        DocumentoProcesadoId = 7, ChunkId = orden, Orden = orden, Score = score, Text = texto,
        MetadataDocumentoNombre = "manual", MetadataDocumentoCodigo = "manual"
    };

    private static RagService Crear(Store store, Emb emb)
    {
        var config = new Mock<IEmbeddingConfiguracionRepository>();
        config.Setup(r => r.GetActivaAsync()).ReturnsAsync((EmbeddingConfiguracion?)null);
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Documento>());
        return new RagService(store, emb, config.Object,
            new Mock<ILogger<RagService>>().Object, repo.Object);
    }

    private static string Relleno(int lineas, string tema) => string.Join("\n",
        Enumerable.Range(0, lineas).Select(i => $"Relleno {tema} numero {i} sin contenido util."));

    [Fact]
    public async Task PuntajesPlanos_RepartenCoberturaPorFragmento_EnOrden()
    {
        // Tres secciones con puntajes casi iguales (0.66/0.64/0.62, dispersión
        // 0.04): ninguna destaca, así que cada fragmento aporta su mejor
        // corrida en vez de concentrar todo en el ganador.
        var consulta = "resumen del manual completo";
        var emb = new Emb { Consulta = consulta };
        emb.learned("Relleno", 0.42);
        emb.learned("Cabecera del documento.", 0.93);
        emb.learned("Cuerpo del documento.", 0.92);
        emb.learned("Trailer del documento.", 0.91);
        var store = new Store(new List<VectorSearchResult>
        {
            Chunk(1, 0.66f, "Cabecera del documento.\n" + Relleno(40, "cabecera")),
            Chunk(2, 0.64f, "Cuerpo del documento.\n" + Relleno(40, "cuerpo")),
            Chunk(3, 0.62f, "Trailer del documento.\n" + Relleno(40, "trailer")),
        });

        var res = await Crear(store, emb).RecuperarContextoDocumentalAsync(consulta, topK: 5);

        Assert.Contains("Cabecera del documento", res.ContextoDocumental);
        Assert.Contains("Cuerpo del documento", res.ContextoDocumental);
        Assert.Contains("Trailer del documento", res.ContextoDocumental);
        // Orden de documento, no de puntaje: el panorama sigue el manual.
        Assert.True(
            res.ContextoDocumental!.IndexOf("Cabecera del documento", StringComparison.Ordinal)
            < res.ContextoDocumental.IndexOf("Cuerpo del documento", StringComparison.Ordinal)
            && res.ContextoDocumental.IndexOf("Cuerpo del documento", StringComparison.Ordinal)
            < res.ContextoDocumental.IndexOf("Trailer del documento", StringComparison.Ordinal));
        // Y el relleno no se cuela por el reparto.
        Assert.DoesNotContain("Relleno cuerpo numero 7", res.ContextoDocumental, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PuntajesConPico_MantienenProfundidad()
    {
// Mismos tres fragmentos pero con pico (0.85 contra 0.55): solo entra
        // la sección ganadora. Es el comportamiento de pregunta enfocada.
        // OJO: las frases son ÚNICAS de este test ("...del manual"): el caché
        // de embeddings es estático por texto exacto y el otro test usa las
        // mismas frases ("...del documento") con otros vectores; compartirlas
        // haría el resultado dependiente del orden de ejecución.
        var consulta = "detalle del cuerpo del manual";
        var emb = new Emb { Consulta = consulta };
        emb.learned("Relleno", 0.42);
        emb.learned("Cabecera del manual.", 0.60);
        emb.learned("Cuerpo del manual.", 0.95);
        emb.learned("Trailer del manual.", 0.58);
var store = new Store(new List<VectorSearchResult>
        {
            Chunk(1, 0.85f, "Cabecera del manual.\n" + Relleno(20, "cabecera")),
            Chunk(2, 0.74f, "Cuerpo del manual.\nMas detalle del cuerpo aqui.\n" + Relleno(20, "cuerpo")),
            Chunk(3, 0.55f, "Trailer del manual.\n" + Relleno(20, "trailer")),
        });

        var res = await Crear(store, emb).RecuperarContextoDocumentalAsync(consulta, topK: 5);

        Assert.Contains("Cuerpo del manual", res.ContextoDocumental);
        Assert.DoesNotContain("Cabecera del manual", res.ContextoDocumental, StringComparison.Ordinal);
    }
}
/// <summary>
/// Plan #15270: "segun sotenibilidad dime las iniciativas actuales". El rescate
/// por typo identifica al documento 's' (código de 1 letra) y se recupera su
/// chunk (0.5425), pero el filtro compartido solo ve el nombre (exige 3+
/// letras) y lo tiraba por el tie-break. El preferido por código exacto
/// siempre sobrevive al corte.
/// </summary>
public class RagPreferenciaPorCodigoTests
{
    private sealed class Store : IVectorStore
    {
        private readonly List<VectorSearchResult> _r;
        public Store(List<VectorSearchResult> r) => _r = r;
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string q, int topK)
            => Task.FromResult(_r.Take(topK).AsEnumerable());
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int topK, VectorSearchFilter f) => SearchAsync(q, topK);
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id) => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync() => Task.FromResult(Enumerable.Empty<int>());
        public Task<string> GetDocumentTextAsync(int id) => Task.FromResult(string.Empty);
        public Task IndexAsync(VectorDocument d) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<VectorDocument> d) => Task.CompletedTask;
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int topK, string n) => SearchAsync(q, topK);
        public Task DeleteDocumentAsync(Guid id) => Task.CompletedTask;
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(0);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task ClearAsync() => Task.CompletedTask;
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync() => Task.FromResult(Enumerable.Empty<(string, int)>());
    }

    private static VectorSearchResult Chunk(int doc, float score, string texto, string nombre, string codigo) => new()
    {
        DocumentoProcesadoId = doc, ChunkId = doc, Orden = doc, Score = score, Text = texto,
        MetadataDocumentoNombre = nombre, MetadataDocumentoCodigo = codigo
    };

    private static RagService Crear(Store store)
    {
        var config = new Mock<IEmbeddingConfiguracionRepository>();
        config.Setup(r => r.GetActivaAsync()).ReturnsAsync((EmbeddingConfiguracion?)null);
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Documento>());
        return new RagService(store, new Mock<IEmbeddingProvider>().Object, config.Object,
            new Mock<ILogger<RagService>>().Object, repo.Object);
    }

    [Fact]
    public async Task PreferenciaPorCodigoCorto_SobreviveAlCorte()
    {
        // El documento 's' puntúa 0.59, por debajo del mejor (0.71) y del 0.70:
        // sin la restitución por código exacto, el tie-break lo elimina aunque
        // sea el preferido explícito.
        var store = new Store(new List<VectorSearchResult>
        {
            Chunk(7, 0.71f, "Texto largo del manual sobre objetos y referencias del archivo PDF.", "ejemplo", "ejemplo"),
            Chunk(9, 0.59f, "Iniciativas Actuales de sostenibilidad del documento.", "s", "s"),
        });

        var res = await Crear(store).RecuperarContextoDocumentalAsync(
            "dime las iniciativas actuales", topK: 5, savedDocumentPreference: "s");

        Assert.Contains("Iniciativas Actuales", res.ContextoDocumental);
    }

    [Fact]
    public async Task SinPreferencia_ElMismoCorteLoElimina()
    {
        // Contracara: sin preferencia explícita, el mismo fragmento de 0.59 sí
        // cae por el tie-break frente al mejor de 0.71. La restitución no es un
        // pase libre: solo protege al preferido.
        var store = new Store(new List<VectorSearchResult>
        {
            Chunk(7, 0.71f, "Texto largo del manual sobre objetos y referencias del archivo PDF.", "ejemplo", "ejemplo"),
            Chunk(9, 0.59f, "Iniciativas Actuales de sostenibilidad del documento.", "s", "s"),
        });

        var res = await Crear(store).RecuperarContextoDocumentalAsync(
            "dime las iniciativas actuales", topK: 5);

        Assert.DoesNotContain("Iniciativas Actuales", res.ContextoDocumental, StringComparison.Ordinal);
    }
}