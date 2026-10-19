using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Asistente.Infrastructure.Services;

/// <summary>
/// Cifrado simétrico AES del contenido de las memorias (ETAPA 21, sección 16).
/// Misma técnica y misma interfaz que <see cref="ConexionCifrador"/> y
/// <see cref="CredencialCifrador"/>.
///
/// Dos diferencias deliberadas frente a esos dos:
///
/// 1. El IV se genera ALEATORIO en cada operación y viaja pegado al cifrado.
///    Los cifradores de conectores derivan el IV de la propia clave y usan el
///    mismo IV siempre. Eso está bien para una cadena de conexión que se
///    cifra una vez, pero no para el contenido de memoria: reutilizar el IV
///    hace que dos textos iguales cifrados se lean idénticos, y en una tabla
///    con muchos registros de "sí" y "no" eso deja adivinar contenido.
///
/// 2. Los valores llevan el prefijo <see cref="Prefijo"/> para distinguirlos de
///    los contenidos escritos antes de que existiera el cifrado y que siguen en
///    claro en la base. <see cref="Descifrar"/> devuelve sin tocar cualquier
///    valor que no empiece por el prefijo: así se puede leer lo anterior al
///    cifrado sin romper, y el backfill de arranque lo codifica después.
///
/// La clave no está en el código: sale de la configuración. Si no hay ninguna,
/// se falla al construir el servicio en lugar de guardar memorias en claro por
/// accidente.
/// </summary>
public class MemoryCifrador : IMemoryCifrador
{
    /// <summary>Marcador que identifica un valor ya cifrado.</summary>
    public const string Prefijo = "enc:v1:";
    private const int LargoIv = 16;
    private readonly byte[] _clave;

    public MemoryCifrador(IConfiguration configuration) : this(ResolverClave(configuration))
    {
    }

    /// <summary>
    /// Fábrica para el <c>DesignTimeDbContextFactory</c>, que arma el contexto en
    /// tiempo de diseño sin contenedor y por tanto sin <see cref="IConfiguration"/>.
    /// </summary>
    public static MemoryCifrador DesdeClave(string clave) => new(clave);

    private MemoryCifrador(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException(
                "Falta la clave de cifrado de memoria: configure Memoria:ClaveCifrado " +
                "(variable MEMORIA_CLAVE_CIFRADO) o JwtSettings:SecretKey (JWT_SECRET). " +
                "Arrancar sin clave guardaría el contenido de las memorias en claro.");

        using var sha = SHA256.Create();
        _clave = sha.ComputeHash(Encoding.UTF8.GetBytes(clave));
    }

    /// <summary>
    /// La clave dedicada manda; si no está, se cae sobre la de JWT. Nada de claves
    /// escritas en el código fuente: solo sale de la configuración.
    /// </summary>
    private static string ResolverClave(IConfiguration configuration)
        => configuration["Memoria:ClaveCifrado"] is { } dedicada && !string.IsNullOrWhiteSpace(dedicada)
            ? dedicada
            : configuration["JwtSettings:SecretKey"] ?? string.Empty;

    public string Cifrar(string textoPlano)
    {
        if (string.IsNullOrEmpty(textoPlano))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _clave;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var ms = new MemoryStream();
        // IV delante: no hace falta que el descifrador recuerde otro sitio dónde
        // leerlo y cada texto cifra distinto aunque el contenido sea el mismo.
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
        using (var writer = new StreamWriter(cs))
        {
            writer.Write(textoPlano);
        }
        return Prefijo + Convert.ToBase64String(ms.ToArray());
    }

    public string Descifrar(string textoGuardado)
    {
        if (string.IsNullOrEmpty(textoGuardado))
            return string.Empty;

        // Sin prefijo: dato anterior al cifrado, se entrega tal cual.
        if (!textoGuardado.StartsWith(Prefijo, StringComparison.Ordinal))
            return textoGuardado;

        byte[] cuerpo;
        try
        {
            cuerpo = Convert.FromBase64String(textoGuardado[Prefijo.Length..]);
        }
        catch (FormatException)
        {
            // Con prefijo pero ilegible: mejor devolverlo y que se vea que algo
            // falló, que tirar una excepción y dejar la lectura entera caída.
            return textoGuardado;
        }

        if (cuerpo.Length <= LargoIv)
            return textoGuardado;

        using var aes = Aes.Create();
        aes.Key = _clave;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = cuerpo[..LargoIv];

        try
        {
            using var ms = new MemoryStream(cuerpo, LargoIv, cuerpo.Length - LargoIv);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            return reader.ReadToEnd();
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "No se pudo descifrar el contenido de una memoria: la clave vigente " +
                "(Memoria:ClaveCifrado) no coincide con la que cifró el dato. " +
                "Recuperar la clave original o restaurar un respaldo antes de seguir.", ex);
        }
    }
}
