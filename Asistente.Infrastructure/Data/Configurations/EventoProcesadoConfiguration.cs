using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class EventoProcesadoConfiguration : IEntityTypeConfiguration<EventoProcesado>
{
    public void Configure(EntityTypeBuilder<EventoProcesado> builder)
    {
        builder.ToTable("EventosProcesados");
        builder.HasKey(e => e.IdEventoProcesado);
        builder.Property(e => e.IdEventoProcesado).ValueGeneratedOnAdd();
        builder.Property(e => e.Estado).IsRequired().HasMaxLength(30);
        builder.Property(e => e.Resultado).HasMaxLength(4000);
        builder.HasOne(e => e.Evento)
            .WithMany()
            .HasForeignKey(e => e.IdEvento)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Workflow)
            .WithMany()
            .HasForeignKey(e => e.IdWorkflow)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.FechaHora);
        builder.HasIndex(e => e.Estado);
    }
}
