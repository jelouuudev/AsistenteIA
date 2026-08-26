using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class WorkflowPasoEjecucionConfiguration : IEntityTypeConfiguration<WorkflowPasoEjecucion>
{
    public void Configure(EntityTypeBuilder<WorkflowPasoEjecucion> builder)
    {
        builder.ToTable("WorkflowPasosEjecucion");
        builder.HasKey(p => p.IdPasoEjecucion);
        builder.Property(p => p.IdPasoEjecucion).ValueGeneratedOnAdd();
        builder.Property(p => p.Resultado).HasColumnType("nvarchar(max)");
        builder.Property(p => p.Estado).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Observaciones).HasMaxLength(2000);
    }
}
