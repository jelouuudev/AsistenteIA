using System.Collections.Generic;
using Asistente.Application.Services.Herramientas;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Regla de relevancia compartida por las dos vías de recuperación. Decide por
/// puntajes, sin vocabulario. Calibración con nomic-embed-text sobre la colección
/// real: el relleno entre documentos ajenos llega a 0.64, los aciertos a 0.58+ y
/// los empates reales bajan a 0.001 (plan #9138: 0.671 vs 0.670).
/// </summary>
public class FiltroRelevanciaTests
{
    [Fact]
    public void Empate_Con_El_Mejor_Se_Conserva_Aunque_Sea_Otro_Documento()
    {
        var keep = SeleccionHerramientaSemantica.FiltrarPorRelevancia(new List<(string, float)>
        {
            ("a", 0.6708f), ("v", 0.6699f), ("ejemplo", 0.533f)
        }, puntajeMinimo: 0.5f);

        Assert.Equal(new[] { 0, 1 }, keep);
    }

    [Fact]
    public void Relleno_De_Otro_Documento_Se_Descarta()
    {
        var keep = SeleccionHerramientaSemantica.FiltrarPorRelevancia(new List<(string, float)>
        {
            ("a", 0.669f), ("ejemplo", 0.555f)
        }, puntajeMinimo: 0.5f);

        Assert.Equal(new[] { 0 }, keep);
    }

    [Fact]
    public void Otra_Fuente_Claramente_Relevante_Se_Conserva()
    {
        var keep = SeleccionHerramientaSemantica.FiltrarPorRelevancia(new List<(string, float)>
        {
            ("ejemplo", 0.81f), ("manual", 0.75f)
        }, puntajeMinimo: 0.5f);

        Assert.Equal(new[] { 0, 1 }, keep);
    }

    [Fact]
    public void Preferido_Sobrevive_Aunque_Quede_Bajo_El_Suelo()
    {
        var keep = SeleccionHerramientaSemantica.FiltrarPorRelevancia(new List<(string, float)>
        {
            ("a", 0.90f), ("ejemplo", 0.40f)
        }, puntajeMinimo: 0.5f, esPreferido: d => d == "ejemplo");

        Assert.Equal(new[] { 0, 1 }, keep);
    }

    [Fact]
    public void Sin_Mejor_Sobre_El_Suelo_No_Se_Rescata_Nada()
    {
        var keep = SeleccionHerramientaSemantica.FiltrarPorRelevancia(new List<(string, float)>
        {
            ("a", 0.55f), ("ejemplo", 0.54f)
        }, puntajeMinimo: 0.6f);

        Assert.Empty(keep);
    }

    [Fact]
    public void Mismo_Documento_Fuera_De_Margen_Se_Descarta()
    {
        var keep = SeleccionHerramientaSemantica.FiltrarPorRelevancia(new List<(string, float)>
        {
            ("a", 0.80f), ("a", 0.60f)
        }, puntajeMinimo: 0.5f);

        Assert.Equal(new[] { 0 }, keep);
    }

    [Fact]
    public void Vacio_Devuelve_Vacio()
    {
        Assert.Empty(SeleccionHerramientaSemantica.FiltrarPorRelevancia(
            new List<(string, float)>(), puntajeMinimo: 0.5f));
    }
}