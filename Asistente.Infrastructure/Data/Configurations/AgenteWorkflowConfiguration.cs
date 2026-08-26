using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AgenteWorkflowConfiguration : IEntityTypeConfiguration<AgenteWorkflow>
{
    public void Configure(EntityTypeBuilder<AgenteWorkflow> builder)
    {
        builder.ToTable("AgentesWorkflows");
        builder.HasKey(aw => aw.IdAgenteWorkflow);

        builder.Property(aw => aw.Activo).IsRequired().HasDefaultValue(true);

        builder.HasOne(aw => aw.Asistente)
            .WithMany(a => a.AgentesWorkflows)
            .HasForeignKey(aw => aw.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(aw => aw.Workflow)
            .WithMany()
            .HasForeignKey(aw => aw.IdWorkflow)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
