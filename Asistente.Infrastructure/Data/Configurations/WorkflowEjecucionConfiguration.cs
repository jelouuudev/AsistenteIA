using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class WorkflowEjecucionConfiguration : IEntityTypeConfiguration<WorkflowEjecucion>
{
    public void Configure(EntityTypeBuilder<WorkflowEjecucion> builder)
    {
        builder.ToTable("WorkflowEjecuciones");
        builder.HasKey(e => e.IdEjecucion);
        builder.Property(e => e.IdEjecucion).ValueGeneratedOnAdd();
        builder.Property(e => e.Estado).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ResultadoFinal).HasColumnType("nvarchar(max)");
        builder.HasMany(e => e.PasosEjecucion)
            .WithOne(p => p.WorkflowEjecucion!)
            .HasForeignKey(p => p.IdEjecucion)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
