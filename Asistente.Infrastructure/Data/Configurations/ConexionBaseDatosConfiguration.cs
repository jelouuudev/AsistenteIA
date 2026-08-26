using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConexionBaseDatosConfiguration : IEntityTypeConfiguration<ConexionBaseDatos>
{
    public void Configure(EntityTypeBuilder<ConexionBaseDatos> builder)
    {
        builder.ToTable("ConexionesBaseDatos");

        builder.HasKey(c => c.IdConexion);

        builder.Property(c => c.IdConexion)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Servidor)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(c => c.BaseDatos)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.UsuarioConexion)
            .HasMaxLength(150)
            .IsRequired(false);

        builder.Property(c => c.CadenaConexionCifrada)
            .HasMaxLength(1000);

        builder.Property(c => c.Activa)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.FechaRegistro)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(c => c.Nombre)
            .IsUnique();
    }
}
