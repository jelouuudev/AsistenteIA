using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> builder)
    {
        builder.ToTable("Permisos");
        builder.HasKey(p => p.IdPermiso);
        builder.Property(p => p.Codigo).IsRequired().HasMaxLength(100);
        builder.HasIndex(p => p.Codigo).IsUnique();
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Modulo).HasMaxLength(50);
    }
}

public class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> builder)
    {
        builder.ToTable("RolPermisos");
        builder.HasKey(rp => new { rp.IdRol, rp.IdPermiso });
        builder.HasOne(rp => rp.Rol).WithMany().HasForeignKey(rp => rp.IdRol).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(rp => rp.Permiso).WithMany().HasForeignKey(rp => rp.IdPermiso).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UsuarioAsistenteConfiguration : IEntityTypeConfiguration<UsuarioAsistente>
{
    public void Configure(EntityTypeBuilder<UsuarioAsistente> builder)
    {
        builder.ToTable("UsuarioAsistentes");
        builder.HasKey(ua => new { ua.IdUsuario, ua.IdAsistente });
        builder.HasOne(ua => ua.Usuario).WithMany().HasForeignKey(ua => ua.IdUsuario).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ua => ua.Asistente).WithMany().HasForeignKey(ua => ua.IdAsistente).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UsuarioFuenteConfiguration : IEntityTypeConfiguration<UsuarioFuente>
{
    public void Configure(EntityTypeBuilder<UsuarioFuente> builder)
    {
        builder.ToTable("UsuarioFuentes");
        builder.HasKey(uf => new { uf.IdUsuario, uf.IdFuente });
        builder.HasOne(uf => uf.Usuario).WithMany().HasForeignKey(uf => uf.IdUsuario).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(uf => uf.Fuente).WithMany().HasForeignKey(uf => uf.IdFuente).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PoliticaIAConfiguration : IEntityTypeConfiguration<PoliticaIA>
{
    public void Configure(EntityTypeBuilder<PoliticaIA> builder)
    {
        builder.ToTable("PoliticasIA");
        builder.HasKey(p => p.IdPolitica);
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Tipo).IsRequired().HasMaxLength(60);
        builder.Property(p => p.Valor).HasMaxLength(2000);
    }
}

public class AuditoriaIAConfiguration : IEntityTypeConfiguration<AuditoriaIA>
{
    public void Configure(EntityTypeBuilder<AuditoriaIA> builder)
    {
        builder.ToTable("AuditoriaIA");
        builder.HasKey(a => a.IdAuditoriaIA);
        builder.Property(a => a.Modelo).HasMaxLength(100);
        builder.Property(a => a.Resultado).HasMaxLength(30);
        builder.HasIndex(a => a.FechaHora);
        builder.HasIndex(a => a.IdUsuario);
    }
}

public class MetricasIAConfiguration : IEntityTypeConfiguration<MetricasIA>
{
    public void Configure(EntityTypeBuilder<MetricasIA> builder)
    {
        builder.ToTable("MetricasIA");
        builder.HasKey(m => m.IdMetrica);
        builder.HasIndex(m => m.FechaHora);
    }
}
