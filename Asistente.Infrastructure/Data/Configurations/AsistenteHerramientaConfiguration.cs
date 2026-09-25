using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AsistenteHerramientaConfiguration : IEntityTypeConfiguration<AsistenteHerramienta>
{
    public void Configure(EntityTypeBuilder<AsistenteHerramienta> builder)
    {
        builder.ToTable("AsistentesHerramientas");
        builder.HasKey(ah => new { ah.IdAsistente, ah.IdHerramienta });

        builder.HasOne(ah => ah.Asistente)
            .WithMany(a => a.AsistentesHerramientas)
            .HasForeignKey(ah => ah.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ah => ah.Herramienta)
            .WithMany(h => h.AsistentesHerramientas)
            .HasForeignKey(ah => ah.IdHerramienta)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
