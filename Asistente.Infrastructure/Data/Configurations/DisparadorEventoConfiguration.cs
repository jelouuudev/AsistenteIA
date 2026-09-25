using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class DisparadorEventoConfiguration : IEntityTypeConfiguration<DisparadorEvento>
{
    public void Configure(EntityTypeBuilder<DisparadorEvento> builder)
    {
        builder.ToTable("DisparadoresEvento");
        builder.HasKey(d => d.IdDisparador);
        builder.Property(d => d.IdDisparador).ValueGeneratedOnAdd();
        builder.Property(d => d.Tipo).IsRequired().HasMaxLength(30);
        builder.Property(d => d.ConfigJson).IsRequired().HasMaxLength(4000);
        builder.HasOne(d => d.Evento)
            .WithMany()
            .HasForeignKey(d => d.IdEvento)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => new { d.IdEvento, d.Activo });
    }
}
