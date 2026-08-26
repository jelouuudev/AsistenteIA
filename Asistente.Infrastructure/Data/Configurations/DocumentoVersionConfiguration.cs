using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class DocumentoVersionConfiguration : IEntityTypeConfiguration<DocumentoVersion>
{
    public void Configure(EntityTypeBuilder<DocumentoVersion> builder)
    {
        builder.ToTable("DocumentoVersion");

        builder.HasKey(v => v.IdVersion);

        builder.Property(v => v.IdVersion)
            .ValueGeneratedOnAdd();

        builder.Property(v => v.NumeroVersion)
            .IsRequired();

        builder.Property(v => v.NombreArchivo)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(v => v.RutaArchivo)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(v => v.TamanoArchivo)
            .IsRequired();

        builder.Property(v => v.HashArchivo)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(v => v.FechaCarga)
            .IsRequired();

        builder.Property(v => v.UsuarioCarga)
            .IsRequired();

        builder.Property(v => v.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(v => v.Documento)
            .WithMany(d => d.Versiones)
            .HasForeignKey(v => v.IdDocumento)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
