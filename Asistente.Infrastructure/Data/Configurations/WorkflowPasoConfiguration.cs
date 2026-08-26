using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class WorkflowPasoConfiguration : IEntityTypeConfiguration<WorkflowPaso>
{
    public void Configure(EntityTypeBuilder<WorkflowPaso> builder)
    {
        builder.ToTable("WorkflowPasos");
        builder.HasKey(p => p.IdPaso);
        builder.Property(p => p.IdPaso).ValueGeneratedOnAdd();
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Herramienta).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Parametros).HasColumnType("nvarchar(max)");
        builder.HasIndex(p => new { p.IdWorkflow, p.Orden });
    }
}
