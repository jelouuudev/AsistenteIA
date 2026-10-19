using System.Text.Json;
using Asistente.Infrastructure.Data;
using Asistente.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Asistente.Infrastructure;

/// <summary>
/// Permite a 'dotnet ef' crear el DbContext en tiempo de diseño sin resolver todos los
/// servicios de la aplicación (vector store, embeddings, etc.) que solo se usan en runtime.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AsistenteDbContext>
{
    public AsistenteDbContext CreateDbContext(string[] args)
    {
        var connectionString = LeerConnectionString()
            ?? "Server=localhost;Database=AsistenteIA;Trusted_Connection=True;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<AsistenteDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        // El modelo cifra Contenido con el conversor de valor, así que el contexto no
        // se puede armar sin cifrador. Se lee la misma clave que usaría el runtime.
        return new AsistenteDbContext(optionsBuilder.Options, MemoryCifrador.DesdeClave(LeerClave()));
    }

    private static string LeerClave()
    {
        var clave = LeerValor("Memoria", "ClaveCifrado") ?? LeerValor("JwtSettings", "SecretKey");
        return clave is { Length: > 0 } ? clave : throw new InvalidOperationException(
            "No se encontró Memoria:ClaveCifrado ni JwtSettings:SecretKey en appsettings: " +
            "el modelo de memoria cifra Contenido y sin clave no se puede construir.");
    }

    private static string? LeerValor(string seccion, string propiedad)
    {
        foreach (var archivo in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            try
            {
                if (!File.Exists(archivo)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(archivo));
                if (doc.RootElement.TryGetProperty(seccion, out var secc)
                    && secc.TryGetProperty(propiedad, out var val)
                    && val.GetString() is { Length: > 0 } texto)
                {
                    return texto;
                }
            }
            catch { /* ignorar y probar el siguiente */ }
        }
        return null;
    }

    private static string? LeerConnectionString()
    {
        foreach (var archivo in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            try
            {
                if (!File.Exists(archivo)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(archivo));
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                    && cs.TryGetProperty("DefaultConnection", out var dc))
                {
                    return dc.GetString();
                }
            }
            catch { /* ignorar y probar el siguiente */ }
        }
        return null;
    }
}
