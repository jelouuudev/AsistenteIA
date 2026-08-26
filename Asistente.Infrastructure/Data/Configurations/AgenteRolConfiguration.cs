using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AgenteRolConfiguration : IEntityTypeConfiguration<AgenteRol>
{
    public void Configure(EntityTypeBuilder<AgenteRol> builder)
    {
        builder.ToTable("AgentesRoles");
        builder.HasKey(ar => ar.IdAgenteRol);

        builder.Property(ar => ar.Activo).IsRequired().HasDefaultValue(true);

        builder.HasOne(ar => ar.Asistente)
            .WithMany(a => a.AgentesRoles)
            .HasForeignKey(ar => ar.IdAsistente)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ar => ar.Rol)
            .WithMany()
            .HasForeignKey(ar => ar.IdRol)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
