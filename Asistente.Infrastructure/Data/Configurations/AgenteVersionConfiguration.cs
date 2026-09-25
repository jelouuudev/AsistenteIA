using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AgenteVersionConfiguration : IEntityTypeConfiguration<AgenteVersion>
{
    public void Configure(EntityTypeBuilder<AgenteVersion> builder)
    {
        builder.ToTable("AgentesVersiones");
        builder.HasKey(av => av.IdAgenteVersion);

        builder.Property(av => av.PromptSistema).HasColumnType("nvarchar(max)");
        builder.Property(av => av.ModeloIA).HasMaxLength(50);
        builder.Property(av => av.Configuracion).HasColumnType("nvarchar(max)");
        builder.Property(av => av.UsuarioCreacion).HasMaxLength(100);
        builder.Property(av => av.Estado).IsRequired().HasDefaultValue(EstadoAgente.Activo);

        builder.HasOne(av => av.Asistente)
            .WithMany(a => a.Versiones)
            .HasForeignKey(av => av.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
