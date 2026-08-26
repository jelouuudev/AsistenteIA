using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Asistente.Infrastructure.Services;

public class ConexionCifrador : IConexionCifrador
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public ConexionCifrador(IConfiguration configuration)
    {
        var key = configuration["MotorConsultas:ClaveCifrado"]
                  ?? "AsistenteIA-Clave-Cifrado-Motor-Consultas-2026";
        var sha = SHA256.Create();
        _key = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        _iv = _key[..16];
    }

    public string Cifrar(string textoPlano)
    {
        if (string.IsNullOrEmpty(textoPlano))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cs))
        {
            writer.Write(textoPlano);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Descifrar(string textoCifrado)
    {
        if (string.IsNullOrEmpty(textoCifrado))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(Convert.FromBase64String(textoCifrado));
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        return reader.ReadToEnd();
    }
}
