using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class DocumentoIndexadoConfiguration : IEntityTypeConfiguration<DocumentoIndexado>
{
    public void Configure(EntityTypeBuilder<DocumentoIndexado> builder)
    {
        builder.ToTable("DocumentoIndexado");

        builder.HasKey(i => i.IdDocumentoIndexado);

        builder.Property(i => i.IdDocumentoIndexado)
            .ValueGeneratedOnAdd();

        builder.Property(i => i.FechaIndexacion)
            .IsRequired();

        builder.Property(i => i.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.TotalChunks)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(i => i.TotalEmbeddings)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(i => i.Observaciones)
            .HasMaxLength(2000);

        builder.HasOne(i => i.DocumentoProcesado)
            .WithMany()
            .HasForeignKey(i => i.IdDocumentoProcesado)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.IdDocumentoProcesado);
        builder.HasIndex(i => i.Estado);
    }
}
