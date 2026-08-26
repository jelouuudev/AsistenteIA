using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConfiguracionRAGConfiguration : IEntityTypeConfiguration<ConfiguracionRAG>
{
    public void Configure(EntityTypeBuilder<ConfiguracionRAG> builder)
    {
        builder.ToTable("ConfiguracionesRAG");

        builder.HasKey(c => c.IdConfiguracion);

        builder.Property(c => c.IdConfiguracion)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.MaxChunks)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(c => c.MaxCaracteresContexto)
            .IsRequired()
            .HasDefaultValue(8000);

        builder.Property(c => c.MinScore)
            .IsRequired()
            .HasDefaultValue(0.45);

        builder.Property(c => c.TopKPorFuente)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(c => c.MaxFuentesConsultadas)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(c => c.MaxReferencias)
            .IsRequired()
            .HasDefaultValue(10);

        builder.Property(c => c.MaxChunksAlModelo)
            .IsRequired()
            .HasDefaultValue(10);

        builder.Property(c => c.UsarDocumentosHistoricos)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(c => c.Activo);
    }
}
