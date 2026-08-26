using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Rol");

        builder.HasKey(r => r.IdRol);

        builder.Property(r => r.IdRol)
            .ValueGeneratedOnAdd();

        builder.Property(r => r.Nombre)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(r => r.Nombre)
            .IsUnique();

        builder.Property(r => r.Descripcion)
            .HasMaxLength(250);

        builder.Property(r => r.Activo)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
