using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConsultaPlantillaConfiguration : IEntityTypeConfiguration<ConsultaPlantilla>
{
    public void Configure(EntityTypeBuilder<ConsultaPlantilla> builder)
    {
        builder.ToTable("ConsultasPlantillas");

        builder.HasKey(p => p.IdPlantilla);

        builder.Property(p => p.IdPlantilla)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Descripcion)
            .HasMaxLength(500);

        builder.Property(p => p.ConsultaSql)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(p => p.Parametros)
            .HasMaxLength(2000);

        builder.Property(p => p.Activa)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.FechaCreacion)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(p => p.Conexion)
            .WithMany()
            .HasForeignKey(p => p.IdConexion)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.Nombre)
            .IsUnique();
    }
}
