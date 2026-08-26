using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ReglaEventoConfiguration : IEntityTypeConfiguration<ReglaEvento>
{
    public void Configure(EntityTypeBuilder<ReglaEvento> builder)
    {
        builder.ToTable("ReglasEvento");
        builder.HasKey(r => r.IdRegla);
        builder.Property(r => r.IdRegla).ValueGeneratedOnAdd();
        builder.Property(r => r.Condicion).HasMaxLength(500);
        builder.HasOne(r => r.Evento)
            .WithMany(e => e.Reglas)
            .HasForeignKey(r => r.IdEvento)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Workflow)
            .WithMany()
            .HasForeignKey(r => r.IdWorkflow)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
