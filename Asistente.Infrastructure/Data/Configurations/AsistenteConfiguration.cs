using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AsistenteConfiguration : IEntityTypeConfiguration<Domain.Entities.Asistente>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Asistente> builder)
    {
        builder.ToTable("Asistente");

        builder.HasKey(a => a.IdAsistente);

        builder.Property(a => a.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Descripcion)
            .HasMaxLength(500);

        builder.Property(a => a.ModeloIA)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("qwen2.5:14b");

        builder.Property(a => a.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.Idioma)
            .HasMaxLength(20);

        builder.Property(a => a.NivelFormalidad)
            .HasMaxLength(50);

        builder.Property(a => a.FormatoRespuesta)
            .HasMaxLength(50);

        builder.Property(a => a.Restricciones)
            .HasMaxLength(1000);

        builder.Property(a => a.MensajeBienvenida)
            .HasMaxLength(500);

        builder.Property(a => a.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Objetivo)
            .HasMaxLength(500);

        builder.Property(a => a.PromptSistema)
            .HasColumnType("nvarchar(max)");

        builder.Property(a => a.Estado)
            .IsRequired()
            .HasDefaultValue(EstadoAgente.Activo);

        builder.Property(a => a.Version)
            .IsRequired()
            .HasDefaultValue(1);

        builder.HasMany(a => a.PromptsSistema)
            .WithOne(p => p.Asistente)
            .HasForeignKey(p => p.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.AsistentesFuentes)
            .WithOne(af => af.Asistente)
            .HasForeignKey(af => af.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.AsistentesHerramientas)
            .WithOne(ah => ah.Asistente)
            .HasForeignKey(ah => ah.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.AgentesWorkflows)
            .WithOne(aw => aw.Asistente)
            .HasForeignKey(aw => aw.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.AgentesRoles)
            .WithOne(ar => ar.Asistente)
            .HasForeignKey(ar => ar.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Versiones)
            .WithOne(v => v.Asistente)
            .HasForeignKey(v => v.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.UsuariosAsistentes)
            .WithOne(ua => ua.Asistente)
            .HasForeignKey(ua => ua.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
