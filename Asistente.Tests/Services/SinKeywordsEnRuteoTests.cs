using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Orchestrator;
using Asistente.Application.Services;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// El proyecto se conecta a varias bases de datos, incluidas nuevas. Ninguna decisión
/// puede depender de un vocabulario escrito en el código: "venta", "cliente",
/// "vacación" o "registrar una factura" no existen en otra base, y ahí el sistema
/// elegiría mal o no ejecutaría nada.
///
/// Estos tests NO comprueban el texto del código: comprueban el COMPORTAMIENTO con
/// entradas que un enrutador por palabras clave no puede acertar. Si alguien reintroduce
/// una lista de términos, estos tests fallan; renombrar una variable no los engaña.
/// </summary>
public class SinKeywordsEnRuteoTests
{
    /// <summary>
    /// Embeddings deterministas por eje de significado, con texto NO determinista: la
    /// pregunta del test no comparte ninguna palabra con los nombres de las herramientas.
    /// </summary>
    private sealed class EmbeddingsPorEje : IEmbeddingProvider
    {
        private const int Dim = 4;
        private readonly Dictionary<string, float[]> _v = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _ejes = new(StringComparer.OrdinalIgnoreCase);

        public EmbeddingsPorEje Eje(string texto, int eje) { _ejes[texto] = eje; return this; }
        public EmbeddingsPorEje Vector(string texto, float[] v) { _v[texto] = v; return this; }

        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (_v.TryGetValue(text, out var propio)) return Task.FromResult(propio);
            var vec = new float[Dim];
            if (_ejes.TryGetValue(text, out var e) && e >= 0 && e < Dim) vec[e] = 1f;
            else vec[Dim - 1] = 0.3f;
            return Task.FromResult(vec);
        }
    }

    private const string DescSql = "Ejecuta consultas SELECT de solo lectura sobre la base de datos empresarial autorizada.";
    private const string DescDoc = "Recupera fragmentos de documentos internos mediante busqueda semantica.";
    private const string DescRep = "Genera reportes estructurados en Markdown a partir de datos obtenidos.";

    private static Mock<IHerramientaRepository> Catalogo() => new();

    // ===== Selección de herramienta por descripción, no por términos =====

    [Fact]
    public async Task SeleccionHerramienta_DecidePorDescripcionYNoPorPalabrasDelMensaje()
    {
        var selector = new SeleccionHerramientaSemantica(new EmbeddingsPorEje()
            .Eje(DescSql, 0).Eje(DescDoc, 1).Eje(DescRep, 2)
            // Mensaje cuyo vocabulario no aparece en NINGUNA descripción ni en ninguna
            // lista de términos del negocio: "escarlatas", "anacapa", "xy". Un selector
            // por substring no encontraría nada; aquí la semántica es la de "consultar
            // datos", así que debe elegir la herramienta de SQL.
            .Vector("quisiera saber quantos escarlatas xy anacapa", new[] { 0.90f, 0.10f, 0.10f, 0.40f }));

        var herramientas = new List<Herramienta>
        {
            new() { Codigo = "SqlQueryTool", Descripcion = DescSql, Categoria = "ConsultaSQL", Activa = true },
            new() { Codigo = "DocumentSearchTool", Descripcion = DescDoc, Categoria = "ConsultaDocumental", Activa = true },
            new() { Codigo = "ReportTool", Descripcion = DescRep, Categoria = "Reporte", Activa = true },
        };

        var elegida = await selector.SeleccionarAsync(
            "quisiera saber quantos escarlatas xy anacapa", herramientas, 0.30);

        Assert.NotNull(elegida);
        Assert.Equal("SqlQueryTool", elegida!.Codigo);
    }

    [Fact]
    public async Task SeleccionHerramienta_NoDisparaSiNingunaDescripcionSeAproxima()
    {
        var selector = new SeleccionHerramientaSemantica(new EmbeddingsPorEje()
            .Eje(DescSql, 0).Eje(DescDoc, 1).Eje(DescRep, 2)
            .Vector("un texto sin relacion con ninguna capacidad", new[] { 0.3f, 0.3f, 0.3f, 0.3f }));

        var herramientas = new List<Herramienta>
        {
            new() { Codigo = "SqlQueryTool", Descripcion = DescSql, Activa = true },
            new() { Codigo = "DocumentSearchTool", Descripcion = DescDoc, Activa = true },
        };

        // Por debajo del umbral no se dispara nada: es preferible que el LLM responda
        // a ejecutar una herramienta equivocada.
        Assert.Null(await selector.SeleccionarAsync("un texto sin relacion con ninguna capacidad", herramientas, 0.62));
    }

    [Fact]
    public async Task SeleccionHerramienta_SinEmbeddings_NoAdivinaPorVocabulario()
    {
        // Sin proveedor de embeddings (Ollama caído) NO se decide por lista de términos.
        var selector = new SeleccionHerramientaSemantica(embeddings: null);
        var herramientas = new List<Herramienta>
        {
            new() { Codigo = "SqlQueryTool", Descripcion = DescSql, Activa = true },
        };

        Assert.Null(await selector.SeleccionarAsync("muéstrame los clientes", herramientas, 0.0));
    }

    [Fact]
    public async Task SeleccionHerramienta_IgnoraHerramientasInactivas()
    {
        var selector = new SeleccionHerramientaSemantica(new EmbeddingsPorEje()
            .Eje(DescSql, 0)
            .Vector("consulta de datos", new[] { 1f, 0f, 0f, 0f }));

        var herramientas = new List<Herramienta>
        {
            new() { Codigo = "SqlQueryTool", Descripcion = DescSql, Activa = false },
        };

        Assert.Null(await selector.SeleccionarAsync("consulta de datos", herramientas, 0.3));
    }

    /// <summary>
    /// Una lista de términos reintroducida como FILTRO que solo INHIBA el disparo
    /// ("si el mensaje contiene cliente/total/producto, no elijas herramienta") es
    /// invisible para los tests positivos: el enrutamiento sigue siendo correcto, solo
    /// que la herramienta deja de dispararse en los casos que sí la requerían.
    ///
    /// Este test lo detecta: el mensaje contiene palabras del vocabulario histórico
    /// (cliente, total, cuánto) y aun así la herramienta de SQL debe dispararse.
    /// </summary>
    [Fact]
    public async Task SeleccionHerramienta_NoSeInhibePorPalabrasDelVocabularioHistorico()
    {
        var selector = new SeleccionHerramientaSemantica(new EmbeddingsPorEje()
            .Eje(DescSql, 0).Eje(DescDoc, 1)
            .Vector("total de clientes activos, cuántos son", new[] { 1f, 0f, 0f, 0f }));

        var herramientas = new List<Herramienta>
        {
            new() { Codigo = "SqlQueryTool", Descripcion = DescSql, Activa = true },
            new() { Codigo = "DocumentSearchTool", Descripcion = DescDoc, Activa = true },
        };

        var elegida = await selector.SeleccionarAsync(
            "total de clientes activos, cuántos son", herramientas, 0.62);

        Assert.NotNull(elegida);
        Assert.Equal("SqlQueryTool", elegida!.Codigo);
    }

    // ===== Restricciones: por significado, no por sinónimos de diccionario =====

    [Fact]
    public async Task Restricciones_ReconocePorSimilitud_NoPorTerminosLiterales()
    {
        var selector = new SeleccionHerramientaSemantica(new EmbeddingsPorEje()
            .Vector("temas de shouldnacion mental", new[] { 1f, 0f, 0f, 0f })
            // El mensaje NO contiene ninguna palabra de la restricción.
            .Vector("me siento mal y necesito ayuda", new[] { 0.9f, 0.1f, 0f, 0f }));

        var sim = await selector.SimilitudAsync("me siento mal y necesito ayuda", "temas de shouldnacion mental");
        Assert.True(sim > 0.75, $"Similitud {sim:F3} deberia superar el umbral de restriccion.");
    }

    // ===== Validación de SQL: estructural, no lista de palabras prohibidas =====

    [Theory]
    [InlineData("SELECT * FROM ventas")]
    [InlineData("SELECT TOP 10 * FROM ventas;")]
    [InlineData("SELECT COUNT(*) FROM ventas WHERE Categoria = 'Ropa'")]
    [InlineData("WITH t AS (SELECT * FROM ventas) SELECT * FROM t")]
    public void Sql_SoloLectura_AceptaConsultasDeLectura(string sql)
        => Assert.True(AnalizadorSql.ValidarSoloLectura(sql).Seguro);

    [Theory]
    [InlineData("DELETE FROM ventas")]
    [InlineData("UPDATE ventas SET Precio = 0")]
    [InlineData("DROP TABLE ventas")]
    [InlineData("INSERT INTO ventas VALUES (1)")]
    [InlineData("EXEC sp_who")]
    [InlineData("SELECT * FROM ventas; DROP TABLE ventas")]
    [InlineData("SELECT * INTO copia FROM ventas")]
    public void Sql_SoloLectura_RechazaEscrituras(string sql)
        => Assert.False(AnalizadorSql.ValidarSoloLectura(sql).Seguro);

    /// <summary>
    /// Regresión del falso positivo: la validación anterior buscaba "update"/"delete"
    /// como SUBCADENA del SQL, así que una columna llamada LastUpdate o IsDeleted
    /// rechazaba una consulta perfectamente de solo lectura.
    /// </summary>
    [Theory]
    [InlineData("SELECT LastUpdate FROM ventas")]
    [InlineData("SELECT IsDeleted, IdVenta FROM ventas")]
    [InlineData("SELECT TotalUpdatedRows FROM ventas")]
    [InlineData("SELECT * FROM ventas WHERE CreatedBy = 'x' AND UpdateTime > '2024-01-01'")]
    public void Sql_SoloLectura_NoRechazaPorNombresDeColumnaConPalabrasDeEscritura(string sql)
        => Assert.True(AnalizadorSql.ValidarSoloLectura(sql).Seguro, AnalizadorSql.ValidarSoloLectura(sql).Motivo);

    /// <summary>Un literal de texto no es gramática: no puede inyectar un verbo.</summary>
    [Fact]
    public void Sql_VerboEnLiteralDeTexto_NoSeInterpretaComoOperacion()
    {
        var r = AnalizadorSql.ValidarSoloLectura("SELECT * FROM ventas WHERE Producto = 'DROP TABLE'");
        Assert.True(r.Seguro, r.Motivo);
    }

    /// <summary>Un comentario no puede ocultar una segunda sentencia.</summary>
    [Fact]
    public void Sql_SentenciaEnComentario_NoSeEjecuta()
    {
        var r = AnalizadorSql.ValidarSoloLectura("SELECT * FROM ventas -- ; DROP TABLE ventas");
        Assert.True(r.Seguro, r.Motivo);
    }

    [Fact]
    public void Sql_ObjetosReferenciados_NoConfundeLiteralConClausula()
    {
        // La palabra FROM dentro de un literal no es una cláusula.
        var objetos = AnalizadorSql.ObjetosReferenciados("SELECT * FROM ventas WHERE Producto = 'FROM pacientes'");
        Assert.Contains("ventas", objetos);
        Assert.DoesNotContain("pacientes", objetos);
    }

    // ===== Contrato de "sin datos": un token, no una lista de frases =====

    [Fact]
    public void Contrato_SinDatos_ReconoceElTokenCompartido()
    {
        var texto = ContratoResultado.MarcarSinDatos("cualquier redacción nueva");
        Assert.True(ContratoResultado.EsSinDatos(texto));
        Assert.True(ContratoResultado.TodosSonSinDatos(texto));
    }

    [Fact]
    public void Contrato_ResultadoMixto_NoEsSinDatos()
    {
        var texto = ContratoResultado.MarcarSinDatos("rama documental vacía")
                    + "\nCategoria: Ropa | Unidades: 25 | Importe: 4285.00";
        Assert.False(ContratoResultado.TodosSonSinDatos(texto));
    }

    [Fact]
    public void Contrato_DatosReales_NoEsSinDatos()
    {
        Assert.False(ContratoResultado.EsSinDatos("Categoria = Ropa: 7 registros, importe total 4285.00"));
    }
}
