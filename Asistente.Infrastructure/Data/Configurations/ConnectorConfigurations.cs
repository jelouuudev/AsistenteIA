using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ConnectorConfiguration : IEntityTypeConfiguration<Connector>
{
    public void Configure(EntityTypeBuilder<Connector> builder)
    {
        builder.ToTable("Connectors");
        builder.HasKey(c => c.IdConnector);
        builder.Property(c => c.Codigo).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Codigo).IsUnique();
        builder.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Tipo).HasMaxLength(30).IsRequired().HasDefaultValue("REST");
        builder.Property(c => c.Descripcion).HasMaxLength(500);
    }
}

public class ConnectorConfigValueConfiguration : IEntityTypeConfiguration<Domain.Entities.ConnectorConfiguration>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.ConnectorConfiguration> builder)
    {
        builder.ToTable("ConnectorConfiguraciones");
        builder.HasKey(c => c.IdConfiguracion);
        builder.Property(c => c.Clave).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Valor).HasMaxLength(1000);
        builder.HasOne(c => c.Connector).WithMany(c => c.Configuraciones)
            .HasForeignKey(c => c.IdConnector).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.IdConnector, c.Clave }).IsUnique();
    }
}

public class ConnectorCredentialEntityConfiguration : IEntityTypeConfiguration<ConnectorCredential>
{
    public void Configure(EntityTypeBuilder<ConnectorCredential> builder)
    {
        builder.ToTable("ConnectorCredenciales");
        builder.HasKey(c => c.IdCredencial);
        builder.Property(c => c.Tipo).HasMaxLength(20).IsRequired().HasDefaultValue("None");
        builder.Property(c => c.NombreUsuario).HasMaxLength(200);
        builder.Property(c => c.ValorCifrado).HasMaxLength(4000);
        builder.Property(c => c.ParametrosCifrados).HasMaxLength(4000);
        builder.HasOne(c => c.Connector).WithMany(c => c.Credenciales)
            .HasForeignKey(c => c.IdConnector).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConnectorPolicyEntityConfiguration : IEntityTypeConfiguration<ConnectorPolicy>
{
    public void Configure(EntityTypeBuilder<ConnectorPolicy> builder)
    {
        builder.ToTable("ConnectorPoliticas");
        builder.HasKey(c => c.IdPolitica);
        builder.HasOne(c => c.Connector).WithMany(c => c.Politicas)
            .HasForeignKey(c => c.IdConnector).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConnectorExecutionEntityConfiguration : IEntityTypeConfiguration<ConnectorExecution>
{
    public void Configure(EntityTypeBuilder<ConnectorExecution> builder)
    {
        builder.ToTable("ConnectorEjecuciones");
        builder.HasKey(c => c.IdEjecucion);
        builder.Property(c => c.Operacion).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Destino).HasMaxLength(1000);
        builder.Property(c => c.Estado).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Error).HasMaxLength(2000);
        builder.Property(c => c.RespuestaResumen).HasMaxLength(2000);
        builder.HasOne(c => c.Connector).WithMany(c => c.Ejecuciones)
            .HasForeignKey(c => c.IdConnector).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.IdConnector, c.Fecha });
    }
}
