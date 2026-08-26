using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class DocumentoFuenteConfiguration : IEntityTypeConfiguration<DocumentoFuente>
{
    public void Configure(EntityTypeBuilder<DocumentoFuente> builder)
    {
        builder.ToTable("DocumentoFuente");

        builder.HasKey(df => new { df.IdDocumento, df.IdFuente });

        builder.Property(df => df.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(df => df.Documento)
            .WithMany(d => d.DocumentosFuentes)
            .HasForeignKey(df => df.IdDocumento)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(df => df.Fuente)
            .WithMany(f => f.DocumentosFuentes)
            .HasForeignKey(df => df.IdFuente)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
