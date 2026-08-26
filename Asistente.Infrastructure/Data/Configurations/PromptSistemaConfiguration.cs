using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class PromptSistemaConfiguration : IEntityTypeConfiguration<PromptSistema>
{
    public void Configure(EntityTypeBuilder<PromptSistema> builder)
    {
        builder.ToTable("PromptSistema");

        builder.HasKey(p => p.IdPrompt);

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Contenido)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(p => p.Version)
            .IsRequired();

        builder.Property(p => p.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.UsuarioCreacion)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasMany(p => p.Historial)
            .WithOne(h => h.Prompt)
            .HasForeignKey(h => h.IdPrompt)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
