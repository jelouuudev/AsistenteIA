using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AuditoriaDocumentalConfiguration : IEntityTypeConfiguration<AuditoriaDocumental>
{
    public void Configure(EntityTypeBuilder<AuditoriaDocumental> builder)
    {
        builder.ToTable("AuditoriaDocumental");

        builder.HasKey(a => a.IdAuditoria);

        builder.Property(a => a.IdAuditoria)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.Accion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Descripcion)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.UsuarioId)
            .IsRequired();

        builder.Property(a => a.FechaAccion)
            .IsRequired();

        builder.Property(a => a.DireccionIP)
            .HasMaxLength(50);

        builder.HasOne(a => a.Documento)
            .WithMany(d => d.Auditorias)
            .HasForeignKey(a => a.IdDocumento)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
