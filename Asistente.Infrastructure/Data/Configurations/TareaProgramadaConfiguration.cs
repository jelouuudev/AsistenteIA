using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class TareaProgramadaConfiguration : IEntityTypeConfiguration<TareaProgramada>
{
    public void Configure(EntityTypeBuilder<TareaProgramada> builder)
    {
        builder.ToTable("TareasProgramadas");
        builder.HasKey(t => t.IdTarea);
        builder.Property(t => t.IdTarea).ValueGeneratedOnAdd();
        builder.Property(t => t.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ExpresionCron).IsRequired().HasMaxLength(100);
        builder.HasOne(t => t.Workflow)
            .WithMany()
            .HasForeignKey(t => t.IdWorkflow)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
