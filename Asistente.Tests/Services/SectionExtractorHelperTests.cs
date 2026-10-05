using Asistente.Application.Services;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Plan #13226: la lista de cabeceras se cortaba en "%PDF-1.5 %PDF" porque la
/// Regex de subsecciones releía la cola de "%PDF-1.6" como encabezado "1.6".
/// La corrección es una clase de caracteres en el lookbehind, sin vocabulario:
/// ningún nombre de versión ni sección aparece en el código.
/// </summary>
public class SectionExtractorHelperTests
{
    [Fact]
    public void VersionAlFinalDeLinea_NoSeConvierteEnSubseccion()
    {
        var entrada = "%PDF-1.0 %PDF-1.1 %PDF-1.2 %PDF-1.3 %PDF-1.4 %PDF-1.5 %PDF-1.6\n"
            + "A partir de la especificación 1.4, la versión de la cabecera puede ser omitida.";

        var salida = SectionExtractorHelper.LimpiarTextoPdf(entrada);

        Assert.Contains("%PDF-1.6", salida);
        Assert.DoesNotContain("**1.6**", salida);
    }

    [Fact]
    public void SubseccionGenuina_SigueMarcandose()
    {
        var entrada = "Introducción general del manual.\n3.1 Recursos Humanos\nEl área gestiona el personal.";

        var salida = SectionExtractorHelper.LimpiarTextoPdf(entrada);

        Assert.Contains("**3.1** Recursos Humanos", salida);
    }

    [Fact]
    public void SeccionPrincipalGenuina_SigueMarcandose()
    {
        var entrada = "Cierre del capítulo anterior.\n4. Trailer de Archivo\nEl trailer permite localizar la tabla.";

        var salida = SectionExtractorHelper.LimpiarTextoPdf(entrada);

        Assert.Contains("**4.** Trailer de Archivo", salida);
    }

    [Fact]
    public void VersionConSlash_NoSeConvierteEnSubseccion()
    {
        // Rutas y versiones con otros separadores tampoco deben partirse.
        var entrada = "Compatible con v2.0/2024 del motor.\nA partir de esa versión se unifica.";

        var salida = SectionExtractorHelper.LimpiarTextoPdf(entrada);

        Assert.Contains("v2.0/2024", salida);
        Assert.DoesNotContain("**2.0**", salida);
    }
}
