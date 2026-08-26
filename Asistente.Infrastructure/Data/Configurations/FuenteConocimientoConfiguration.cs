using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class FuenteConocimientoConfiguration : IEntityTypeConfiguration<FuenteConocimiento>
{
    public void Configure(EntityTypeBuilder<FuenteConocimiento> builder)
    {
        builder.ToTable("FuenteConocimiento");

        builder.HasKey(f => f.IdFuente);

        builder.Property(f => f.IdFuente)
            .ValueGeneratedOnAdd();

        builder.Property(f => f.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(f => f.Codigo)
            .IsUnique();

        builder.Property(f => f.Descripcion)
            .HasMaxLength(1000);

        builder.Property(f => f.Tipo)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(f => f.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(f => f.Prioridad)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(f => f.FechaCreacion)
            .IsRequired();

        builder.Property(f => f.UsuarioCreacion)
            .IsRequired();

        builder.HasMany(f => f.AsistentesFuentes)
            .WithOne(af => af.Fuente)
            .HasForeignKey(af => af.IdFuente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.DocumentosFuentes)
            .WithOne(df => df.Fuente)
            .HasForeignKey(df => df.IdFuente)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
