using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// El scoring léxico no debe privilegiar ninguna palabra: "objetivo" (que
/// estuvo en una lista fija de términos) tiene que puntuar igual que cualquier
/// otra palabra con la misma densidad. La lista se eliminó; este test impide
/// que vuelva por otra vía.
/// </summary>
public class RecuperacionSinKeywordsTests
{
    private static DocumentoChunk Chunk(string texto) => new() { Texto = texto };

    [Fact]
    public void TerminoDeLaAntiguaLista_PuntuaIgualQueCualquierOtro()
    {
        var terminos = new List<string> { "objetivo", "trapecio" };
        var chunks = new List<DocumentoChunk>
        {
            Chunk("hablamos del objetivo central del documento"),
            Chunk("hablamos del trapecio central del documento"),
        };

        var puntuados = RecuperacionService.PuntuarLexico(
            "objetivo trapecio", terminos, chunks, CancellationToken.None)
            .ToDictionary(p => p.chunk.Texto, p => p.score);

        Assert.Equal(
            puntuados["hablamos del trapecio central del documento"],
            puntuados["hablamos del objetivo central del documento"]);
    }

    [Fact]
    public void FraseSolucionDeProblemas_NoRecibeBoostEspecial()
    {
        // La frase completa "solución de problemas" tenía +15 puntos fijos.
        var terminos = new List<string> { "solucion", "problema", "reparacion", "averia" };
        var chunks = new List<DocumentoChunk>
        {
            Chunk("manual de solucion de problema para el equipo"),
            Chunk("manual de reparacion de averia para el equipo"),
        };

        var puntuados = RecuperacionService.PuntuarLexico(
            "solucion problema", terminos, chunks, CancellationToken.None)
            .ToDictionary(p => p.chunk.Texto, p => p.score);

        // "solucion"+"problema" (2/4) vs "reparacion"+"averia" (2/4): misma
        // densidad, mismo puntaje. Antes el primero recibía +15 por frase fija.
        Assert.Equal(
            puntuados["manual de reparacion de averia para el equipo"],
            puntuados["manual de solucion de problema para el equipo"]);
    }
}
