using Asistente.Application.Services;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Asistente.Infrastructure.Services;

public class ExtractoraTextoService : IExtractoraTextoService
{
    private readonly ILogger<ExtractoraTextoService> _logger;

    public ExtractoraTextoService(ILogger<ExtractoraTextoService> logger)
    {
        _logger = logger;
    }

    public Task<ExtraccionPdfResult> ExtraerTextoPdfAsync(string rutaArchivo)
    {
        try
        {
            if (!File.Exists(rutaArchivo))
            {
                return Task.FromResult(new ExtraccionPdfResult
                {
                    Exitoso = false,
                    Error = $"El archivo no existe: {rutaArchivo}"
                });
            }

            var textoCompleto = new System.Text.StringBuilder();
            var totalPaginas = 0;

            using var document = PdfDocument.Open(rutaArchivo);

            totalPaginas = document.NumberOfPages;

            foreach (Page page in document.GetPages())
            {
                var textoPagina = ExtraerTextoConEspaciado(page);
                if (!string.IsNullOrWhiteSpace(textoPagina))
                {
                    textoCompleto.AppendLine(textoPagina);
                    textoCompleto.AppendLine();
                }
            }

            // LIMPIAR TEXTO PDF AQUÍ - antes de guardar en BD/vector store
            var textoExtraido = textoCompleto.ToString().Trim();
            // Eliminar null bytes que pueden causar errores
            textoExtraido = textoExtraido.Replace("\0", string.Empty);
            var textoLimpio = SectionExtractorHelper.LimpiarTextoPdf(textoExtraido);

            var resultado = new ExtraccionPdfResult
            {
                Exitoso = true,
                TotalPaginas = totalPaginas,
                TextoCompleto = textoLimpio,
                EsDocumentoProtegido = false
            };

            _logger.LogInformation("Texto extraído exitosamente de '{Archivo}': {Paginas} páginas, {Caracteres} caracteres",
                Path.GetFileName(rutaArchivo), totalPaginas, resultado.TextoCompleto.Length);

            return Task.FromResult(resultado);
        }
        catch (Exception ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Documento protegido con contraseña: '{Archivo}'", Path.GetFileName(rutaArchivo));
            return Task.FromResult(new ExtraccionPdfResult
            {
                Exitoso = false,
                Error = "El documento está protegido con contraseña.",
                EsDocumentoProtegido = true
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error al leer el documento PDF: '{Archivo}'", Path.GetFileName(rutaArchivo));
            return Task.FromResult(new ExtraccionPdfResult
            {
                Exitoso = false,
                Error = $"Error de lectura: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al procesar PDF: '{Archivo}'", Path.GetFileName(rutaArchivo));
            return Task.FromResult(new ExtraccionPdfResult
            {
                Exitoso = false,
                Error = $"Error inesperado: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Extrae texto de una página PDF respetando espacios entre palabras y saltos de línea
    /// según la posición de los glifos (evita "GitHub" partido o palabras pegadas).
    /// </summary>
    private static string ExtraerTextoConEspaciado(Page page)
    {
        var words = page.GetWords()?.ToList();
        if (words == null || words.Count == 0)
            return page.Text ?? string.Empty;

        // Orden lectura: arriba→abajo, izquierda→derecha
        var ordenadas = words
            .OrderByDescending(w => Math.Round(w.BoundingBox.Bottom, 1))
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var sb = new System.Text.StringBuilder();
        double? prevBottom = null;
        double? prevRight = null;
        double avgHeight = ordenadas.Average(w => w.BoundingBox.Height);
        if (avgHeight < 1) avgHeight = 10;

        foreach (var word in ordenadas)
        {
            var text = word.Text?.Trim();
            if (string.IsNullOrEmpty(text))
                continue;

            double bottom = word.BoundingBox.Bottom;
            double left = word.BoundingBox.Left;
            double right = word.BoundingBox.Right;

            if (prevBottom.HasValue)
            {
                // Nueva línea si el salto vertical es significativo
                if (Math.Abs(prevBottom.Value - bottom) > avgHeight * 0.6)
                {
                    sb.AppendLine();
                    prevRight = null;
                }
                else if (prevRight.HasValue)
                {
                    // Espacio entre palabras de la misma línea
                    double gap = left - prevRight.Value;
                    double avgWidth = Math.Max(word.BoundingBox.Width / Math.Max(text.Length, 1), 1);
                    if (gap > avgWidth * 0.15)
                        sb.Append(' ');
                }
            }

            sb.Append(text);
            prevBottom = bottom;
            prevRight = right;
        }

        return sb.ToString();
    }

    public Task<ExtraccionPdfResult> ExtraerTextoAsync(string rutaArchivo)
    {
        try
        {
            if (!File.Exists(rutaArchivo))
            {
                return Task.FromResult(new ExtraccionPdfResult
                {
                    Exitoso = false,
                    Error = $"El archivo no existe: {rutaArchivo}"
                });
            }

            var extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();

            if (extension == ".txt")
            {
                var texto = File.ReadAllText(rutaArchivo);
                var resultado = new ExtraccionPdfResult
                {
                    Exitoso = true,
                    TotalPaginas = 1,
                    TextoCompleto = texto,
                    EsDocumentoProtegido = false
                };

                _logger.LogInformation("Texto extraído exitosamente de archivo TXT '{Archivo}': {Caracteres} caracteres",
                    Path.GetFileName(rutaArchivo), resultado.TextoCompleto.Length);

                return Task.FromResult(resultado);
            }
            else if (extension == ".pdf")
            {
                return ExtraerTextoPdfAsync(rutaArchivo);
            }
            else
            {
                return Task.FromResult(new ExtraccionPdfResult
                {
                    Exitoso = false,
                    Error = $"Formato de archivo no soportado: {extension}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al procesar archivo: '{Archivo}'", Path.GetFileName(rutaArchivo));
            return Task.FromResult(new ExtraccionPdfResult
            {
                Exitoso = false,
                Error = $"Error inesperado: {ex.Message}"
            });
        }
    }
}
