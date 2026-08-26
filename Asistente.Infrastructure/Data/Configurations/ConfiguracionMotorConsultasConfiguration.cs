using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConfiguracionMotorConsultasConfiguration : IEntityTypeConfiguration<ConfiguracionMotorConsultas>
{
    public void Configure(EntityTypeBuilder<ConfiguracionMotorConsultas> builder)
    {
        builder.ToTable("ConfiguracionesMotorConsultas");

        builder.HasKey(c => c.IdConfiguracion);

        builder.Property(c => c.IdConfiguracion)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.TiempoMaximoEjecucionSegundos)
            .IsRequired()
            .HasDefaultValue(15);

        builder.Property(c => c.MaximoRegistros)
            .IsRequired()
            .HasDefaultValue(100);

        builder.Property(c => c.MaxConsultasSimultaneas)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne<ConexionBaseDatos>()
            .WithMany()
            .HasForeignKey(c => c.IdConexionPredeterminada)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.Activo);
    }
}
