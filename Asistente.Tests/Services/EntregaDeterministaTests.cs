using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Entrega final determinista del plan: si el contexto previo ya trae el reporte
/// consolidado de ReportTool, se entrega sin LLM (cero latencia, cero typos,
/// cero fuga de instrucciones). Sin reporte, null (usar LLM como antes).
/// </summary>
public class EntregaDeterministaTests
{
    private const string ReporteHogar =
        "# Generar resumen ejecutivo / informe\n" +
        "\n" +
        "_Generado: 01/10/2026 19:37_\n" +
        "Datos obtenidos de la tabla 'ventas' (base de datos 'VentasTest'). Desglose por Categoria:\n" +
        "- Categoria = Hogar: 7 registros, 22 unidades, importe total 4445.00\n";

    [Fact]
    public void Extrae_ElUltimoReporte_DelContexto()
    {
        var contexto =
            "[Paso 1: Consultar datos]\nDatos obtenidos de la tabla 'ventas'. Total: 7.\n" +
            "[Paso 3: Generar resumen]\n" + ReporteHogar;

        var entrega = AgentOrchestrator.ExtraerReporteConsolidado(contexto);

        Assert.NotNull(entrega);
        Assert.StartsWith("# Generar resumen", entrega);
        Assert.Contains("importe total 4445.00", entrega);
        Assert.DoesNotContain("[Paso 1", entrega);
    }

    [Fact]
    public void SinReporte_DevuelveNull_ParaUsarLLM()
    {
        var contexto = "[Paso 1: Consultar datos]\nDatos obtenidos de la tabla 'ventas'. Total: 7.";

        Assert.Null(AgentOrchestrator.ExtraerReporteConsolidado(contexto));
        Assert.Null(AgentOrchestrator.ExtraerReporteConsolidado(null));
        Assert.Null(AgentOrchestrator.ExtraerReporteConsolidado("   "));
    }

    [Fact]
    public void ReporteSinDatos_EcoaPregunta_DevuelveNull_ParaUsarLLM()
    {
        // Plan #7056: ReportTool sin insumo devolvió el título + la pregunta.
        // Atajar al LLM aquí entregaría el eco; null deja que el LLM responda
        // desde el contexto completo (que sí trae lo documental).
        var eco = "# Generar resumen ejecutivo / informe\n\n_Generado: 01/10/2026 23:16_\n" +
            "segun el archivo ejemplo, cuales son los Componentes de un archivo PDF?";

        Assert.Null(AgentOrchestrator.ExtraerReporteConsolidado(eco));
    }

    [Fact]
    public void Determinista_NoAlteraCifras()
    {
        var contexto = "ruido previo\n" + ReporteHogar.Replace("4445.00", "4285.00");

        var entrega = AgentOrchestrator.ExtraerReporteConsolidado(contexto);

        Assert.NotNull(entrega);
        Assert.Contains("4285.00", entrega);
        Assert.DoesNotContain("4445.00", entrega);
    }

    [Fact]
    public void Sanea_FugaDeInstrucciones_DelLLM()
    {
        var respuestaProto =
            "**Total de registros:** 24\n" +
            "INSTRUCCIONES OBLIGATORIAS:\n" +
            "- Usa SOLO los datos del CONTEXTO anterior. No inventes números ni valores.\n" +
            "- Prohibido inventar. Responde lo que SÍ esté en el contexto.\n" +
            "## CONTEXT DE DATOS REALES (proporcionado por pasos anteriores):\n" +
            "- Categoria = Ropa: 7 registros, 25 unidades, importe total 4285.00";

        var limpia = AgentOrchestrator.SanearEntrega(respuestaProto);

        Assert.Contains("**Total de registros:** 24", limpia);
        Assert.Contains("importe total 4285.00", limpia);
        Assert.DoesNotContain("INSTRUCCIONES OBLIGATORIAS", limpia);
        Assert.DoesNotContain("CONTEXT DE DATOS REALES", limpia);
    }

    /// <summary>
    /// Plan #9066: contexto mixto (desglose SQL + sección RAG) SIN reporte
    /// consolidado. Antes iba al LLM, que inventaba ítems ("Pruéncipes").
    /// Ahora se fusiona determinísticamente: cifras SQL + RAG verbatim.
    /// </summary>
    [Fact]
    public void MixtoSinReporte_FusionDeterminista_SinLLM()
    {
        var contexto =
            "Datos obtenidos de la tabla 'activos' (base de datos 'ControlActivosTest'). Desglose por Responsable (cifras calculadas en SQL, no recalcular). Filtro aplicado: WHERE [Responsable] = 'Juan Pérez'.\n" +
            "- Responsable = Juan Pérez: 4 registros, suma de la columna decimal 1870.00\n" +
            "Según **ejemplo**: un Objeto Diccionario es una tabla asociativa con pares Clave - Valor.";

        var entrega = AgentOrchestrator.EntregaEstructuradaDeterminista(contexto);

        Assert.NotNull(entrega);
        Assert.Contains("Juan Pérez", entrega);
        Assert.Contains("4 registros", entrega);
        Assert.Contains("Objeto Diccionario es una tabla asociativa", entrega);
    }

    [Fact]
    public void ConversacionLibre_DevuelveNull_ParaUsarLLM()
    {
        Assert.Null(AgentOrchestrator.EntregaEstructuradaDeterminista(
            "hola, ¿cómo estás? cuéntame un chiste"));
        Assert.Null(AgentOrchestrator.EntregaEstructuradaDeterminista(null));
    }

    /// <summary>
    /// Split documental de pregunta mixta por comprensión (plan #9072): el ranking
    /// RAG ya no debe ver la mitad SQL. Sin listas: micro-LLM genérico.
    /// </summary>
    [Fact]
    public async Task Mixta_ExtraeSoloParteDocumental()
    {
        var ollama = new Mock<IOllamaService>();
        ollama.Setup(o => o.SendMessageAsync(
                It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"documental\":\"háblame del trailer de archivo\"}");

        var parte = await AgentOrchestrator.ExtraerParteDocumentalAsync(
            ollama.Object,
            "háblame del trailer de archivo, y dime cuantos productos son de categoria electronica?",
            CancellationToken.None);

        Assert.Equal("háblame del trailer de archivo", parte);
    }

    /// <summary>
    /// Split de datos de pregunta mixta por comprensión (plan #9152): la rama SQL
    /// ya no debe ver la mitad documental ("Metas para 2027" contaminaba el filtro
    /// numérico). Simétrico al split documental. Sin listas: micro-LLM genérico.
    /// </summary>
    [Fact]
    public async Task Mixta_ExtraeSoloParteDatos()
    {
        var ollama = new Mock<IOllamaService>();
        ollama.Setup(o => o.SendMessageAsync(
                It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"datos\":\"muestrame los insumos que se miden en litros\"}");

        var parte = await AgentOrchestrator.ExtraerParteDatosAsync(
            ollama.Object,
            "dime las Metas para 2027, y muestrame los insumos que se miden en litros, parte datos split",
            CancellationToken.None);

        Assert.Equal("muestrame los insumos que se miden en litros", parte);
    }

    [Fact]
    public void EntradaRag_ExtraeDocumentoPreferido()
    {
        Assert.Equal("s", AgentOrchestrator.ExtraerDocumentoPreferido("{\"documento\":\"s\"}"));
        Assert.Null(AgentOrchestrator.ExtraerDocumentoPreferido(null));
        Assert.Null(AgentOrchestrator.ExtraerDocumentoPreferido("texto plano de rama"));
    }

    [Fact]
    public async Task MicroLlmFalla_DevuelveObjetivoIntegro()
    {
        var ollama = new Mock<IOllamaService>();
        ollama.Setup(o => o.SendMessageAsync(
                It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sin json aquí");
        const string objetivo = "háblame del trailer de archivo?";

        var parte = await AgentOrchestrator.ExtraerParteDocumentalAsync(
            ollama.Object, objetivo, CancellationToken.None);

        Assert.Equal(objetivo, parte);
        Assert.Equal(objetivo, await AgentOrchestrator.ExtraerParteDocumentalAsync(
            null, objetivo, CancellationToken.None));
    }

    [Fact]
    public void MixtoConRamaSqlVacia_NoMuestraTokenCrudo()
    {
        var contexto =
            "Datos obtenidos de la tabla 'activos':\n[ SIN_DATOS ] la consulta no devolvió filas\n".Replace("[ SIN_DATOS ]", "[SIN_DATOS]") +
            "Según **ejemplo**: un Objeto Diccionario es una tabla asociativa.";

        var entrega = AgentOrchestrator.EntregaEstructuradaDeterminista(contexto);

        Assert.NotNull(entrega);
        Assert.DoesNotContain("[SIN_DATOS]", entrega);
        Assert.Contains("Objeto Diccionario", entrega);
    }
}

/// <summary>
/// El paso "Clasificar por nivel de riesgo" se detecta por plantilla propia
/// (la genera el PlanBuilder), igual que el modo por rol: nunca por el
/// lenguaje del usuario.
/// </summary>
public class PasoDeRiesgoTests
{
    [Theory]
    [InlineData("Clasificar por nivel de riesgo", true)]
    [InlineData("CLASIFICAR POR NIVEL DE RIESGO", true)]
    [InlineData("Analizar resultados y calcular indicadores", false)]
    [InlineData("Entregar resultado final al usuario", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void EsPasoDeRiesgo_SoloPlantillaPropia(string? nombre, bool esperado)
    {
        Assert.Equal(esperado, AgentOrchestrator.EsPasoDeRiesgo(nombre));
    }
}
/// <summary>
/// El presupuesto de la clasificación de riesgo no puede quedar por debajo de lo
/// que tarda el LLM en CPU (deepseek-r1:7b ≈ 80-150 s): con 60 s configurados el
/// paso caía al determinista y repetía el informe (#1007, #1008).
/// </summary>
public class PresupuestoClasificacionTests
{
    [Theory]
    [InlineData(60000, 300000)]
    [InlineData(1, 300000)]
    [InlineData(0, 300000)]
    [InlineData(300000, 300000)]
    [InlineData(420000, 420000)]
    [InlineData(600000, 600000)]
    [InlineData(900000, 900000)]
    public void CalcularPresupuesto_RespetaPisoYCongurado(int configurado, int esperado)
    {
        Assert.Equal(esperado, AgentOrchestrator.CalcularPresupuestoClasificacion(configurado));
    }

    [Fact]
    public void CalcularPresupuesto_NuncaSuperaElCtsDelPlanDe15Min()
    {
        Assert.True(AgentOrchestrator.CalcularPresupuestoClasificacion(900000) <= 15 * 60 * 1000);
    }
}
/// <summary>
/// Groundaje de la clasificación de riesgo (#1009). deepseek-r1:7b declaraba
/// "25 vs 10 por debajo del mínimo" (comparación invertida) y repetía el mismo
/// hallazgo. El control es estructural: formato, cifras existentes y dirección de
/// la comparación. Sin vocabulario de dominio.
/// </summary>
public class GroundajeRiesgoTests
{
    private const string Contexto = """
        | IdInsumo | Nombre       | Stock | StockMinimo |
        | 1        | Arroz Extra  | 120   | 50          |
        | 2        | Aji Amarillo | 15    | 20          |
        | 7        | Queso Andino | 8     | 10          |
        """;

    [Fact]
    public void DescartaComparacionInvertida_ValoresDelContexto()
    {
        // Arroz Extra: stock 120 y mínimo 50, o sea 120 >= 50. Afirmar que está
        // "por debajo del mínimo" es una comparación al revés y debe descartarse.
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "- [ALTO] hecho: Arroz Extra 120 < 50 stock mínimo: riesgo de desabastecimiento", Contexto);

        Assert.Equal(string.Empty, r);
    }

    [Fact]
    public void ConservaComparacionCorrecta()
    {
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "- [ALTO] hecho: Queso Andino 8 < 10: stock por debajo del mínimo", Contexto);

        Assert.Contains("[ALTO]", r);
        Assert.Contains("Queso Andino", r);
    }

    [Fact]
    public void DescartaCifraInexistenteEnElContexto()
    {
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "- [ALTO] hecho: 999 unidades comprometidas: riesgo de quiebre", Contexto);

        Assert.Equal(string.Empty, r);
    }

    [Fact]
    public void EliminaRiesgosDuplicados()
    {
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "- [ALTO] hecho: Queso Andino 8 < 10: bajo mínimo\n"
            + "- [MEDIO] hecho: Queso Andino 8 < 10: otro motivo", Contexto);

        var ocurrencias = r.Split('\n').Count(l => l.Contains("Queso Andino"));
        Assert.Equal(1, ocurrencias);
    }

    [Fact]
    public void IgnoraLineasSinFormatoDeNivel()
    {
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "Este es un resumen general del inventario.", Contexto);

        Assert.Equal(string.Empty, r);
    }

    [Fact]
    public void AceptaRespuestaDeclaradaSinRiesgos()
    {
        var r = AgentOrchestrator.ValidarGroundajeRiesgo("Sin riesgos evidentes en los datos.", Contexto);

        Assert.Equal("Sin riesgos evidentes en los datos.", r);
    }

    [Fact]
    public void AceptaNivelSinViñetaYConEnfasis()
    {
        // El modelo 7B no siempre escribe "- [ALTO]": en el caso #1012 devolvió otra
        // forma y el filtro descartó el 100%. Estas variantes deben aceptarse.
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "**[ALTO]** Queso Andino 8 < 10: bajo mínimo", Contexto);

        Assert.Contains("- [ALTO]", r);
        Assert.Contains("Queso Andino", r);
    }

    [Fact]
    public void AceptaNivelSinViñetaNiComilla()
    {
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "[MEDIO] Aji Amarillo 15 < 20: por debajo del mínimo", Contexto);

        Assert.Contains("- [MEDIO]", r);
    }

    [Fact]
    public void IgnoraLineasDeEncabezadoYResumen()
    {
        // Encabezados markdown y texto sin nivel no son riesgos: no deben colarse.
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "## Clasificación de riesgos\n\nEste inventario no tiene problemas graves.\n"
            + "- [ALTO] Queso Andino 8 < 10: bajo mínimo", Contexto);

        var lineas = r.Split('\n').Where(l => l.Contains("inventario") || l.Contains("##")).ToList();
        Assert.Empty(lineas);
        Assert.Contains("[ALTO]", r);
    }

    [Fact]
    public void NormalizaMilesAlCompararCifras()
    {
        var ctx = "| Total | 2,723.50 |";
        var r = AgentOrchestrator.ValidarGroundajeRiesgo(
            "- [ALTO] hecho: 2,723.50 comprometida en un solo pago", ctx);

        Assert.Contains("2,723.50", r);
    }
}