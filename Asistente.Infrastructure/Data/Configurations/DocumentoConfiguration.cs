using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("Documento");

        builder.HasKey(d => d.IdDocumento);

        builder.Property(d => d.IdDocumento)
            .ValueGeneratedOnAdd();

        builder.Property(d => d.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(d => d.Codigo)
            .HasFilter("[Estado] != 'Eliminado'")
            .IsUnique();

        builder.Property(d => d.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Descripcion)
            .HasMaxLength(1000);

        builder.Property(d => d.VersionActual)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(d => d.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.PendienteProcesamiento)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(d => d.FechaRegistro)
            .IsRequired();

        builder.Property(d => d.UsuarioRegistro)
            .IsRequired();

        builder.HasOne(d => d.Categoria)
            .WithMany(c => c.Documentos)
            .HasForeignKey(d => d.IdCategoria)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
