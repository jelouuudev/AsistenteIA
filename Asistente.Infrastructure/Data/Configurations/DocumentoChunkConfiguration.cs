using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class DocumentoChunkConfiguration : IEntityTypeConfiguration<DocumentoChunk>
{
    public void Configure(EntityTypeBuilder<DocumentoChunk> builder)
    {
        builder.ToTable("DocumentoChunk");

        builder.HasKey(c => c.IdChunk);

        builder.Property(c => c.IdChunk)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.NumeroChunk)
            .IsRequired();

        builder.Property(c => c.PaginaInicial)
            .IsRequired();

        builder.Property(c => c.PaginaFinal)
            .IsRequired();

        builder.Property(c => c.Texto)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.TotalCaracteres)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.Orden)
            .IsRequired();

        builder.HasOne(c => c.DocumentoProcesado)
            .WithMany(p => p.Chunks)
            .HasForeignKey(c => c.IdDocumentoProcesado)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.IdDocumentoProcesado);
    }
}
