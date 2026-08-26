using System.Text.Json;
using Asistente.Infrastructure.Data;
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

        return new AsistenteDbContext(optionsBuilder.Options);
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
