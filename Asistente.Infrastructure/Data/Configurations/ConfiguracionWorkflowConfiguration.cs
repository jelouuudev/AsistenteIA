using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConfiguracionWorkflowConfiguration : IEntityTypeConfiguration<ConfiguracionWorkflow>
{
    public void Configure(EntityTypeBuilder<ConfiguracionWorkflow> builder)
    {
        builder.ToTable("ConfiguracionWorkflow");
        builder.HasKey(c => c.IdConfiguracion);
        builder.Property(c => c.IdConfiguracion).ValueGeneratedNever();
    }
}
