using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows");
        builder.HasKey(w => w.IdWorkflow);
        builder.Property(w => w.IdWorkflow).ValueGeneratedOnAdd();
        builder.Property(w => w.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(w => w.Codigo).IsRequired().HasMaxLength(100);
        builder.Property(w => w.Descripcion).HasMaxLength(1000);
        builder.Property(w => w.Disparadores).HasMaxLength(1000);
        builder.HasIndex(w => w.Codigo).IsUnique();
        builder.HasMany(w => w.Pasos)
            .WithOne(p => p.Workflow!)
            .HasForeignKey(p => p.IdWorkflow)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
