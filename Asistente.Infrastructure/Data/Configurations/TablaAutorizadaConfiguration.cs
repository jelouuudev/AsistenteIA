using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class TablaAutorizadaConfiguration : IEntityTypeConfiguration<TablaAutorizada>
{
    public void Configure(EntityTypeBuilder<TablaAutorizada> builder)
    {
        builder.ToTable("TablasAutorizadas");

        builder.HasKey(t => t.IdTabla);

        builder.Property(t => t.IdTabla)
            .ValueGeneratedOnAdd();

        builder.Property(t => t.NombreTabla)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(t => t.Esquema)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Descripcion)
            .HasMaxLength(500);

        builder.Property(t => t.Activa)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(t => t.Conexion)
            .WithMany(c => c.TablasAutorizadas)
            .HasForeignKey(t => t.IdConexion)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.IdConexion, t.Esquema, t.NombreTabla })
            .IsUnique();
    }
}
