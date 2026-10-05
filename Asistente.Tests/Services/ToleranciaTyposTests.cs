using System.Collections.Generic;
using Asistente.Application.Services.Herramientas;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Tolerancia a typos por métrica de edición (Jaro-Winkler), sin vocabulario:
/// compara palabras de la pregunta contra identificadores vivos del catálogo
/// (código, nombre, archivo). Calibración con el caso real del plan #10170.
/// </summary>
public class ToleranciaTyposTests
{
    [Fact]
    public void Typo_Real_Supera_El_Umbral()
    {
        // "sotenibilidad" (pregunta) vs "sostenibilidad" (sostenibilidad_v1.pdf).
        var sim = FiltroSemantico.JaroWinkler("sotenibilidad", "sostenibilidad");

        Assert.True(sim >= 0.85, $"Similitud {sim:F4} bajo el umbral 0.85");
    }

    [Theory]
    // Palabras comunes de preguntas contra tokens reales del catálogo
    // (sostenibilidad, vacaciones, ejemplo, astronomia): no deben confundirse.
    [InlineData("mercado", "ejemplo")]
    [InlineData("tienen", "sostenibilidad")]
    [InlineData("mercado", "vacaciones")]
    [InlineData("cuantos", "astronomia")]
    [InlineData("dime", "domain")]
    [InlineData("vacaciones", "sostenibilidad")]
    public void Palabras_Comunes_No_Superan_El_Umbral(string a, string b)
    {
        Assert.True(FiltroSemantico.JaroWinkler(a, b) < 0.85,
            $"'{a}' vs '{b}' = {FiltroSemantico.JaroWinkler(a, b):F4}");
    }

    [Fact]
    public void Identico_Devuelve_Uno_Vacio_Devuelve_Cero()
    {
        Assert.Equal(1.0, FiltroSemantico.JaroWinkler("sostenibilidad", "sostenibilidad"));
        Assert.Equal(0.0, FiltroSemantico.JaroWinkler("", "sostenibilidad"));
        Assert.Equal(0.0, FiltroSemantico.JaroWinkler("abc", ""));
    }

    [Fact]
    public void TokensAlfanumericos_Parte_El_Archivo_En_Tokens_Largos()
    {
        var tokens = FiltroSemantico.TokensAlfanumericos("sostenibilidad_v1.pdf");

        Assert.Contains("sostenibilidad", tokens);
        Assert.DoesNotContain("v1", tokens);
        Assert.DoesNotContain("pdf", tokens);
    }

    [Fact]
    public void TokensAlfanumericos_Codigos_Cortos_Se_Excluyen()
    {
        Assert.Empty(FiltroSemantico.TokensAlfanumericos("s"));
        Assert.Empty(FiltroSemantico.TokensAlfanumericos("a"));
    }
}