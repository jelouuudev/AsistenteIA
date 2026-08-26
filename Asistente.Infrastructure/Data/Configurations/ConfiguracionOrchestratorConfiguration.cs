using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConfiguracionOrchestratorConfiguration : IEntityTypeConfiguration<ConfiguracionOrchestrator>
{
    public void Configure(EntityTypeBuilder<ConfiguracionOrchestrator> builder)
    {
        builder.ToTable("ConfiguracionOrchestrator");
        builder.HasKey(c => c.IdConfiguracion);
        builder.Property(c => c.IdConfiguracion).ValueGeneratedOnAdd();
    }
}
