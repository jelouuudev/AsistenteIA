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
    }
}
