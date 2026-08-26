using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AsistenteFuenteConfiguration : IEntityTypeConfiguration<AsistenteFuente>
{
    public void Configure(EntityTypeBuilder<AsistenteFuente> builder)
    {
        builder.ToTable("AsistenteFuente");

        builder.HasKey(af => new { af.IdAsistente, af.IdFuente });

        builder.Property(af => af.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(af => af.Prioridad)
            .IsRequired()
            .HasDefaultValue(5);

        builder.HasOne(af => af.Asistente)
            .WithMany(a => a.AsistentesFuentes)
            .HasForeignKey(af => af.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(af => af.Fuente)
            .WithMany(f => f.AsistentesFuentes)
            .HasForeignKey(af => af.IdFuente)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
