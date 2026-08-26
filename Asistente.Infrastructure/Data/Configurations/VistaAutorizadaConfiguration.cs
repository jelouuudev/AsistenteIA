using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class VistaAutorizadaConfiguration : IEntityTypeConfiguration<VistaAutorizada>
{
    public void Configure(EntityTypeBuilder<VistaAutorizada> builder)
    {
        builder.ToTable("VistasAutorizadas");

        builder.HasKey(v => v.IdVista);

        builder.Property(v => v.IdVista)
            .ValueGeneratedOnAdd();

        builder.Property(v => v.NombreVista)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(v => v.Descripcion)
            .HasMaxLength(500);

        builder.Property(v => v.Activa)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(v => v.Conexion)
            .WithMany(c => c.VistasAutorizadas)
            .HasForeignKey(v => v.IdConexion)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.IdConexion, v.NombreVista })
            .IsUnique();
    }
}
