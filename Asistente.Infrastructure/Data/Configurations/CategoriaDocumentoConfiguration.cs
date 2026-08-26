using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class CategoriaDocumentoConfiguration : IEntityTypeConfiguration<CategoriaDocumento>
{
    public void Configure(EntityTypeBuilder<CategoriaDocumento> builder)
    {
        builder.ToTable("CategoriaDocumento");

        builder.HasKey(c => c.IdCategoria);

        builder.Property(c => c.IdCategoria)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(c => c.Nombre)
            .IsUnique();

        builder.Property(c => c.Descripcion)
            .HasMaxLength(500);

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
