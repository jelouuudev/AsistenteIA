using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConversacionConfiguration : IEntityTypeConfiguration<Conversacion>
{
    public void Configure(EntityTypeBuilder<Conversacion> builder)
    {
        builder.ToTable("Conversacion");

        builder.HasKey(c => c.IdConversacion);

        builder.Property(c => c.IdConversacion)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.FechaInicio)
            .IsRequired();

        builder.Property(c => c.FechaFin);

        builder.Property(c => c.Estado)
            .IsRequired()
            .HasConversion(new EnumToStringConverter<EstadoConversacion>())
            .HasMaxLength(20);

        builder.Property(c => c.Titulo)
            .HasMaxLength(200);

        builder.Property(c => c.UsuarioPropietario)
            .IsRequired();

        builder.Property(c => c.FechaUltimaActividad);

        builder.Property(c => c.ResumenContexto)
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.TotalMensajes)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.IdAsistente);
        
        builder.Property(c => c.UltimoDocumentoPreferido)
            .HasMaxLength(100);

        builder.HasIndex(c => c.UsuarioPropietario);
        builder.HasIndex(c => c.FechaUltimaActividad);
    }
}
