using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class EjecucionHerramientaConfiguration : IEntityTypeConfiguration<EjecucionHerramienta>
{
    public void Configure(EntityTypeBuilder<EjecucionHerramienta> builder)
    {
        builder.ToTable("EjecucionesHerramientas");
        builder.HasKey(e => e.IdEjecucion);

        builder.Property(e => e.Parametros)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.Resultado)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.Estado)
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue("Pendiente");

        builder.HasOne(e => e.Herramienta)
            .WithMany()
            .HasForeignKey(e => e.IdHerramienta)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.FechaHora);
    }
}
