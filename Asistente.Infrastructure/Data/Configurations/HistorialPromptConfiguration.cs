using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class HistorialPromptConfiguration : IEntityTypeConfiguration<HistorialPrompt>
{
    public void Configure(EntityTypeBuilder<HistorialPrompt> builder)
    {
        builder.ToTable("HistorialPrompt");

        builder.HasKey(h => h.IdHistorial);

        builder.Property(h => h.Contenido)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(h => h.UsuarioModificacion)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(h => h.MotivoCambio)
            .HasMaxLength(500);
    }
}
