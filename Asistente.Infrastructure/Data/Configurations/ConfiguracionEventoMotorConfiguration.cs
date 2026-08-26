using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConfiguracionEventoMotorConfiguration : IEntityTypeConfiguration<ConfiguracionEventoMotor>
{
    public void Configure(EntityTypeBuilder<ConfiguracionEventoMotor> builder)
    {
        builder.ToTable("ConfiguracionEventoMotor");
        builder.HasKey(c => c.IdConfiguracion);
        // Id fijo = 1 (registro único de configuración global).
        builder.Property(c => c.IdConfiguracion).ValueGeneratedNever();
    }
}
