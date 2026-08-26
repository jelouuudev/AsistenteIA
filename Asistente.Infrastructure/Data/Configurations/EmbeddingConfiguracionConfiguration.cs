using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class EmbeddingConfiguracionConfiguration : IEntityTypeConfiguration<EmbeddingConfiguracion>
{
    public void Configure(EntityTypeBuilder<EmbeddingConfiguracion> builder)
    {
        builder.ToTable("EmbeddingConfiguracion");

        builder.HasKey(c => c.IdConfiguracion);

        builder.Property(c => c.IdConfiguracion)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.Proveedor)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ModeloEmbeddings)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.BaseVectorial)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.CantidadResultados)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(c => c.PuntajeMinimo)
            .IsRequired()
            .HasDefaultValue(0.5);

        builder.Property(c => c.LongitudMaximaContexto)
            .IsRequired()
            .HasDefaultValue(4000);

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(c => c.Activo);
    }
}
