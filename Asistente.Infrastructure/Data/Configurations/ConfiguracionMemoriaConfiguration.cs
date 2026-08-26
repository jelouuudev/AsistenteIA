using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConfiguracionMemoriaConfiguration : IEntityTypeConfiguration<ConfiguracionMemoria>
{
    public void Configure(EntityTypeBuilder<ConfiguracionMemoria> builder)
    {
        builder.ToTable("ConfiguracionMemoria");

        builder.HasKey(c => c.IdConfiguracion);

        builder.Property(c => c.IdConfiguracion)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.MaximoMensajesContexto)
            .IsRequired()
            .HasDefaultValue(20);

        builder.Property(c => c.MaximoTokensContexto)
            .IsRequired()
            .HasDefaultValue(4096);

        builder.Property(c => c.LongitudResumen)
            .IsRequired()
            .HasDefaultValue(500);

        builder.Property(c => c.CantidadConversacionesVisibles)
            .IsRequired()
            .HasDefaultValue(50);

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
