using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConsultaEjecutadaConfiguration : IEntityTypeConfiguration<ConsultaEjecutada>
{
    public void Configure(EntityTypeBuilder<ConsultaEjecutada> builder)
    {
        builder.ToTable("ConsultasEjecutadas");

        builder.HasKey(c => c.IdConsulta);

        builder.Property(c => c.IdConsulta)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.FechaHora)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(c => c.PreguntaUsuario)
            .HasMaxLength(1000);

        builder.Property(c => c.OperacionEjecutada)
            .HasMaxLength(200);

        builder.Property(c => c.ConsultaGenerada)
            .HasMaxLength(4000);

        builder.Property(c => c.Estado)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Resultado)
            .HasMaxLength(4000);

        builder.HasOne(c => c.Usuario)
            .WithMany()
            .HasForeignKey(c => c.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Conexion)
            .WithMany(c => c.ConsultasEjecutadas)
            .HasForeignKey(c => c.IdConexion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.FechaHora);
        builder.HasIndex(c => c.IdUsuario);
        builder.HasIndex(c => c.IdConexion);
    }
}
