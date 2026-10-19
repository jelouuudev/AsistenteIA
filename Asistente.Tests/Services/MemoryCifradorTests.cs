using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Asistente.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Cifrado del contenido de memoria (ETAPA 21, sección 16).
///
/// Estos tests fijan lo que se decidió al implementarlo y lo que se pudo haber
/// hecho mal sin que nada fallara: si el cifrado se cae en claro, todos los tests
/// del resto de la suite siguen pasando porque prueban el texto ya descifrado.
/// </summary>
public class MemoryCifradorTests
{
    private static MemoryCifrador Cifrador(string clave = "clave-de-prueba-para-memory-cifrador")
        => new(ConClave(clave));

    private static IConfiguration ConClave(string clave)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Memoria:ClaveCifrado"] = clave
            })
            .Build();

    [Fact]
    public void Cifrar_Y_Descifrar_DevuelveElTextoOriginal()
    {
        const string texto = "El formato del reporte mensual tiene 6 columnas y va el día 5.";

        var guardado = Cifrador().Cifrar(texto);

        Assert.Equal(texto, Cifrador().Descifrar(guardado));
    }

    /// <summary>
    /// La prueba obvia y la más fácil de pasar por accidente: el texto no puede
    /// verse en la columna. Si alguien cambiara el cifrado por un "cifrar" que
    /// devuelve el texto tal cual, el roundtrip seguiría pasando.
    /// </summary>
    [Fact]
    public void Cifrar_NoDejaElTextoLegible()
    {
        const string texto = "Credencial_de_cliente_muy_reservada";

        var guardado = Cifrador().Cifrar(texto);

        Assert.DoesNotContain(texto, guardado);
        Assert.StartsWith(MemoryCifrador.Prefijo, guardado);
    }

    /// <summary>
    /// IV aleatorio por operación: dos veces lo mismo cifra distinto.
    ///
    /// Es la diferencia con ConexionCifrador y CredencialCifrador, que derivan el
    /// IV de la clave y usan el mismo siempre. Con contenido de memoria eso dejaría
    /// adivinar: esta tabla llena de "Sí"/"No" mostraría qué filas son iguales.
    /// </summary>
    [Fact]
    public void Cifrar_DosVecesElMismoTexto_DaDistinto()
    {
        const string texto = "Sí";

        var primero = Cifrador().Cifrar(texto);
        var segundo = Cifrador().Cifrar(texto);

        Assert.NotEqual(primero, segundo);
        // Y aun así ambos vuelven al mismo contenido: el IV cambia, el texto no.
        Assert.Equal(texto, Cifrador().Descifrar(primero));
        Assert.Equal(texto, Cifrador().Descifrar(segundo));
    }

    /// <summary>
    /// Compatibilidad con lo que ya estaba en la base antes de cifrar. Sin esto,
    /// cualquier instalación con memorias previas empezaría a lanzar excepciones de
    /// criptografía en el primer arranque tras el cambio.
    /// </summary>
    [Fact]
    public void Descifrar_SinPrefijo_DevuelveElValorTalCual()
    {
        const string textoAntiguo = "Contenido guardado en claro antes del cifrado";

        Assert.Equal(textoAntiguo, Cifrador().Descifrar(textoAntiguo));
    }

    [Fact]
    public void CadenaVacia_SigueVacia_NoSeCifraNada()
    {
        Assert.Equal(string.Empty, Cifrador().Cifrar(string.Empty));
        Assert.Equal(string.Empty, Cifrador().Descifrar(string.Empty));
    }

    /// <summary>
    /// Preferible fallar al arrancar a guardar el contenido de las memorias en
    /// claro por accidente: el contenido institucional entraría a la base sin que
    /// nada lo avisara.
    /// </summary>
    [Fact]
    public void SinClave_NiEnLaSeccionNiEnJwt_Falla()
    {
        var vacia = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new MemoryCifrador(vacia));
        Assert.Contains("Memoria:ClaveCifrado", ex.Message);
    }

    /// <summary>
    /// Si no hay clave dedicada se cae sobre la de JWT: así funciona quien ya tiene
    /// JWT configurado sin obligarle a tocar nada, y quien quiera aislarla puede.
    /// </summary>
    [Fact]
    public void SinClaveDedicada_CaeSobreLaDeJwt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "clave-de-jwt-para-respaldo-del-cifrado"
            })
            .Build();

        var cifrador = new MemoryCifrador(config);
        Assert.Equal("algo", cifrador.Descifrar(cifrador.Cifrar("algo")));
    }

    /// <summary>
    /// Otra clave no descifra. Aquí se comprueba además que el error dice qué
    /// pasó: un "padding is invalid" suelto no le dice a nadie que cambió la clave.
    /// </summary>
    [Fact]
    public void ConOtraClave_NoDescifra_YExplicaQueCambioLaClave()
    {
        var guardado = Cifrador("clave-primera").Cifrar("contenido protegido");

        var ex = Assert.Throws<CryptographicException>(
            () => Cifrador("clave-otra-distinta").Descifrar(guardado));

        Assert.Contains("Memoria:ClaveCifrado", ex.Message);
        Assert.IsType<CryptographicException>(ex.InnerException);
    }
}
