using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class EventoEmpresarialConfiguration : IEntityTypeConfiguration<EventoEmpresarial>
{
    public void Configure(EntityTypeBuilder<EventoEmpresarial> builder)
    {
        builder.ToTable("EventosEmpresariales");
        builder.HasKey(e => e.IdEvento);
        builder.Property(e => e.IdEvento).ValueGeneratedOnAdd();
        builder.Property(e => e.Codigo).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Descripcion).HasMaxLength(1000);
        builder.Property(e => e.Categoria).IsRequired().HasMaxLength(50);
        builder.HasIndex(e => e.Codigo).IsUnique();
        builder.HasMany(e => e.Reglas)
            .WithOne(r => r.Evento!)
            .HasForeignKey(r => r.IdEvento)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
