using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class HerramientaConfiguration : IEntityTypeConfiguration<Herramienta>
{
    public void Configure(EntityTypeBuilder<Herramienta> builder)
    {
        builder.ToTable("Herramientas");
        builder.HasKey(h => h.IdHerramienta);

        builder.Property(h => h.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(h => h.Codigo)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(h => h.Codigo)
            .IsUnique();

        builder.Property(h => h.Descripcion)
            .HasMaxLength(500);

        builder.Property(h => h.Categoria)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("Utilidad");
    }
}
