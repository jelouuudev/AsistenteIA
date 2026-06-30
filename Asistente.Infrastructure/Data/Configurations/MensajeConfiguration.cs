using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Asistente.Infrastructure.Data.Configurations;

public class MensajeConfiguration : IEntityTypeConfiguration<Mensaje>
{
    public void Configure(EntityTypeBuilder<Mensaje> builder)
    {
        builder.ToTable("Mensaje");

        builder.HasKey(m => m.IdMensaje);

        builder.Property(m => m.IdMensaje)
            .ValueGeneratedOnAdd();

        builder.Property(m => m.IdConversacion)
            .IsRequired();

        builder.Property(m => m.Rol)
            .IsRequired()
            .HasConversion(new EnumToStringConverter<RolMensaje>())
            .HasMaxLength(20);

        builder.Property(m => m.Contenido)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.FechaHora)
            .IsRequired();

        builder.Property(m => m.TiempoRespuestaMs);

        builder.HasOne(m => m.Conversacion)
            .WithMany(c => c.Mensajes)
            .HasForeignKey(m => m.IdConversacion)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
