using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AuditoriaActividadConfiguration : IEntityTypeConfiguration<AuditoriaActividad>
{
    public void Configure(EntityTypeBuilder<AuditoriaActividad> builder)
    {
        builder.ToTable("AuditoriaActividad");

        builder.HasKey(a => a.IdActividad);

        builder.Property(a => a.IdActividad)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.FechaHora)
            .IsRequired();

        builder.Property(a => a.Modulo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Accion)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.TipoOperacion)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(a => a.Resultado)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(a => a.Descripcion)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.DireccionIP)
            .HasMaxLength(50);

        builder.HasOne(a => a.Usuario)
            .WithMany(u => u.AuditoriasActividad)
            .HasForeignKey(a => a.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
