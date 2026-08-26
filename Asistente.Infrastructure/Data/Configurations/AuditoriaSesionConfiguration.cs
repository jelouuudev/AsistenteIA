using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AuditoriaSesionConfiguration : IEntityTypeConfiguration<AuditoriaSesion>
{
    public void Configure(EntityTypeBuilder<AuditoriaSesion> builder)
    {
        builder.ToTable("AuditoriaSesion");

        builder.HasKey(s => s.IdSesion);

        builder.Property(s => s.IdSesion)
            .ValueGeneratedOnAdd();

        builder.Property(s => s.FechaInicio)
            .IsRequired();

        builder.Property(s => s.FechaFin);

        builder.Property(s => s.DireccionIP)
            .HasMaxLength(50);

        builder.Property(s => s.Navegador)
            .HasMaxLength(250);

        builder.Property(s => s.Estado)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasOne(s => s.Usuario)
            .WithMany(u => u.AuditoriasSesion)
            .HasForeignKey(s => s.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
