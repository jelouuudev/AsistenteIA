using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class DocumentoProcesadoConfiguration : IEntityTypeConfiguration<DocumentoProcesado>
{
    public void Configure(EntityTypeBuilder<DocumentoProcesado> builder)
    {
        builder.ToTable("DocumentoProcesado");

        builder.HasKey(p => p.IdDocumentoProcesado);

        builder.Property(p => p.IdDocumentoProcesado)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.FechaInicio)
            .IsRequired();

        builder.Property(p => p.FechaFin)
            .IsRequired(false);

        builder.Property(p => p.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.TotalPaginas)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.TotalCaracteres)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.TotalChunks)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.Observaciones)
            .HasMaxLength(2000);

        builder.HasOne(p => p.VersionDocumento)
            .WithMany(v => v.Procesamientos)
            .HasForeignKey(p => p.IdVersionDocumento)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.IdVersionDocumento);
        builder.HasIndex(p => p.Estado);
    }
}
