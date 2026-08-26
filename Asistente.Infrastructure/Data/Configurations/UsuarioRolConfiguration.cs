using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class UsuarioRolConfiguration : IEntityTypeConfiguration<UsuarioRol>
{
    public void Configure(EntityTypeBuilder<UsuarioRol> builder)
    {
        builder.ToTable("UsuarioRol");

        builder.HasKey(ur => new { ur.IdUsuario, ur.IdRol });

        builder.HasOne(ur => ur.Usuario)
            .WithMany(u => u.UsuarioRoles)
            .HasForeignKey(ur => ur.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Rol)
            .WithMany(r => r.UsuarioRoles)
            .HasForeignKey(ur => ur.IdRol)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
