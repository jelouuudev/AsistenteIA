using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario");

        builder.HasKey(u => u.IdUsuario);

        builder.Property(u => u.IdUsuario)
            .ValueGeneratedOnAdd();

        builder.Property(u => u.UsuarioNombre)
            .HasColumnName("Usuario")
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => u.UsuarioNombre)
            .IsUnique();

        builder.Property(u => u.Nombres)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Apellidos)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Correo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.FechaCreacion)
            .IsRequired();

        builder.Property(u => u.FechaUltimoAcceso);
    }
}
