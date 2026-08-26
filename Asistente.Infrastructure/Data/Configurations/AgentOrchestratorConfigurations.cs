using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class AgentExecutionConfiguration : IEntityTypeConfiguration<AgentExecution>
{
    public void Configure(EntityTypeBuilder<AgentExecution> builder)
    {
        builder.ToTable("AgentExecution");
        builder.HasKey(e => e.IdExecution);
        builder.Property(e => e.Pregunta).IsRequired();
        builder.Property(e => e.Estado).HasMaxLength(20).IsRequired();
        builder.HasOne(e => e.AgentePrincipal)
            .WithMany()
            .HasForeignKey(e => e.IdAgentePrincipal)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Pasos)
            .WithOne(p => p.Ejecucion)
            .HasForeignKey(p => p.IdExecution)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Trazas)
            .WithOne(t => t.Ejecucion)
            .HasForeignKey(t => t.IdExecution)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgentExecutionStepConfiguration : IEntityTypeConfiguration<AgentExecutionStep>
{
    public void Configure(EntityTypeBuilder<AgentExecutionStep> builder)
    {
        builder.ToTable("AgentExecutionStep");
        builder.HasKey(s => s.IdStep);
        builder.Property(s => s.Accion).IsRequired();
        builder.Property(s => s.Estado).HasMaxLength(20).IsRequired();
        builder.HasOne(s => s.Agente)
            .WithMany()
            .HasForeignKey(s => s.IdAgente)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AgentCollaborationRuleConfiguration : IEntityTypeConfiguration<AgentCollaborationRule>
{
    public void Configure(EntityTypeBuilder<AgentCollaborationRule> builder)
    {
        builder.ToTable("AgentCollaborationRule");
        builder.HasKey(r => r.IdRule);
        builder.HasIndex(r => new { r.AgenteOrigen, r.AgenteDestino });
        builder.HasOne(r => r.Origen)
            .WithMany()
            .HasForeignKey(r => r.AgenteOrigen)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Destino)
            .WithMany()
            .HasForeignKey(r => r.AgenteDestino)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AgentExecutionTraceConfiguration : IEntityTypeConfiguration<AgentExecutionTrace>
{
    public void Configure(EntityTypeBuilder<AgentExecutionTrace> builder)
    {
        builder.ToTable("AgentExecutionTrace");
        builder.HasKey(t => t.IdTrace);
        builder.Property(t => t.Evento).IsRequired();
        builder.HasOne(t => t.Ejecucion)
            .WithMany(e => e.Trazas)
            .HasForeignKey(t => t.IdExecution)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
