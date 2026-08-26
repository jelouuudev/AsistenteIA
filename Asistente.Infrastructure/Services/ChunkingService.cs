using System;
using System.Collections.Generic;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Infrastructure.Services;

public class ChunkingService : IChunkingService
{
    public List<DocumentoChunk> DividirEnChunks(
        string textoNormalizado,
        int idDocumentoProcesado,
        ChunkingConfig config)
    {
        if (string.IsNullOrWhiteSpace(textoNormalizado))
            return new List<DocumentoChunk>();

        var chunks = new List<DocumentoChunk>();
        var textoLimpio = textoNormalizado;

        // Dividir por párrafos primero
        var parrafos = textoLimpio.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var chunkActual = new System.Text.StringBuilder();
        var numeroChunk = 1;
        var orden = 1;
        var caracteresAcumulados = 0;

        foreach (var parrafo in parrafos)
        {
            var parrafoTrimmed = parrafo.Trim();
            if (string.IsNullOrEmpty(parrafoTrimmed))
                continue;

            // Si el párrafo solo cabe completo
            if (parrafoTrimmed.Length <= config.TamanoMaximoChunk)
            {
                // Verificar si agregar este párrafo excede el límite
                var textoPotencial = chunkActual.Length > 0
                    ? chunkActual.ToString() + "\n\n" + parrafoTrimmed
                    : parrafoTrimmed;

                if (textoPotencial.Length <= config.TamanoMaximoChunk)
                {
                    if (chunkActual.Length > 0)
                        chunkActual.Append("\n\n");
                    chunkActual.Append(parrafoTrimmed);
                    caracteresAcumulados += parrafoTrimmed.Length;
                }
                else
                {
                    // Guardar chunk actual y empezar uno nuevo
                    if (chunkActual.Length > 0 && chunkActual.Length >= config.LongitudMinima)
                    {
                        chunks.Add(CrearChunk(chunkActual.ToString(), numeroChunk, idDocumentoProcesado, orden, config));
                        numeroChunk++;
                        orden++;
                    }

                    chunkActual.Clear();
                    chunkActual.Append(parrafoTrimmed);
                    caracteresAcumulados = parrafoTrimmed.Length;
                }
            }
            else
            {
                // El párrafo es muy largo, dividir por oraciones
                if (chunkActual.Length > 0 && chunkActual.Length >= config.LongitudMinima)
                {
                    chunks.Add(CrearChunk(chunkActual.ToString(), numeroChunk, idDocumentoProcesado, orden, config));
                    numeroChunk++;
                    orden++;
                    chunkActual.Clear();
                    caracteresAcumulados = 0;
                }

                var oraciones = DividirEnOraciones(parrafoTrimmed);

                foreach (var oracion in oraciones)
                {
                    if (string.IsNullOrEmpty(oracion.Trim()))
                        continue;

                    var textoPotencial = chunkActual.Length > 0
                        ? chunkActual.ToString() + " " + oracion.Trim()
                        : oracion.Trim();

                    if (textoPotencial.Length <= config.TamanoMaximoChunk)
                    {
                        if (chunkActual.Length > 0)
                            chunkActual.Append(" ");
                        chunkActual.Append(oracion.Trim());
                        caracteresAcumulados += oracion.Trim().Length;
                    }
                    else
                    {
                        // Guardar chunk actual
                        if (chunkActual.Length >= config.LongitudMinima)
                        {
                            chunks.Add(CrearChunk(chunkActual.ToString(), numeroChunk, idDocumentoProcesado, orden, config));
                            numeroChunk++;
                            orden++;
                        }

                        // Aplicar solapamiento
                        var textoPrevio = chunkActual.ToString();
                        chunkActual.Clear();

                        if (config.Solapamiento > 0 && textoPrevio.Length > config.Solapamiento)
                        {
                            var textoSolapado = textoPrevio.Substring(textoPrevio.Length - config.Solapamiento);
                            // Asegurar que no cortar palabras en el solapamiento
                            var espacioIdx = textoSolapado.IndexOf(' ');
                            if (espacioIdx > 0)
                                textoSolapado = textoSolapado.Substring(espacioIdx + 1);
                            chunkActual.Append(textoSolapado);
                            chunkActual.Append(" ");
                        }

                        chunkActual.Append(oracion.Trim());
                        caracteresAcumulados = chunkActual.Length;
                    }
                }
            }
        }

        // Agregar último chunk pendiente
        if (chunkActual.Length > 0)
        {
            if (chunkActual.Length >= config.LongitudMinima)
            {
                chunks.Add(CrearChunk(chunkActual.ToString(), numeroChunk, idDocumentoProcesado, orden, config));
            }
            else if (chunks.Count > 0)
            {
                // Si el último chunk es muy pequeño, fusionarlo con el anterior
                var ultimoChunk = chunks[chunks.Count - 1];
                ultimoChunk.Texto += "\n\n" + chunkActual.ToString();
                ultimoChunk.TotalCaracteres = ultimoChunk.Texto.Length;
            }
            else
            {
                // Único chunk, incluso si es pequeño
                chunks.Add(CrearChunk(chunkActual.ToString(), numeroChunk, idDocumentoProcesado, orden, config));
            }
        }

        return chunks;
    }

    private static DocumentoChunk CrearChunk(
        string texto,
        int numeroChunk,
        int idDocumentoProcesado,
        int orden,
        ChunkingConfig config)
    {
        return new DocumentoChunk
        {
            IdDocumentoProcesado = idDocumentoProcesado,
            NumeroChunk = numeroChunk,
            PaginaInicial = 1,
            PaginaFinal = 1,
            Texto = texto.Trim(),
            TotalCaracteres = texto.Trim().Length,
            Orden = orden
        };
    }

    private static List<string> DividirEnOraciones(string texto)
    {
        var oraciones = new List<string>();
        var separadores = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };

        var partes = texto.Split(separadores, StringSplitOptions.RemoveEmptyEntries);

        foreach (var parte in partes)
        {
            var oracion = parte.Trim();
            if (!string.IsNullOrEmpty(oracion))
            {
                // Recuperar el signo de puntuación
                if (!oracion.EndsWith('.') && !oracion.EndsWith('!') && !oracion.EndsWith('?'))
                {
                    oracion += ".";
                }
                oraciones.Add(oracion);
            }
        }

        return oraciones;
    }
}
