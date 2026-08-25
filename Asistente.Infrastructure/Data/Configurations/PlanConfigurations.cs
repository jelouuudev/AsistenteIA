using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plan");
        builder.HasKey(p => p.IdPlan);
        builder.Property(p => p.Objetivo).IsRequired();
        builder.Property(p => p.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("Borrador");
        builder.Property(p => p.Razonamiento).HasColumnType("nvarchar(max)");
        builder.HasMany(p => p.Pasos)
            .WithOne(s => s.Plan)
            .HasForeignKey(s => s.IdPlan)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Dependencias)
            .WithOne(d => d.Plan)
            .HasForeignKey(d => d.IdPlan)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Logs)
            .WithOne(l => l.Plan)
            .HasForeignKey(l => l.IdPlan)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanStepConfiguration : IEntityTypeConfiguration<PlanStep>
{
    public void Configure(EntityTypeBuilder<PlanStep> builder)
    {
        builder.ToTable("PlanStep");
        builder.HasKey(s => s.IdStep);
        builder.Property(s => s.Tipo).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Nombre).IsRequired();
        builder.Property(s => s.Estado).HasMaxLength(20).IsRequired().HasDefaultValue("Pendiente");
        builder.Property(s => s.Resultado).HasColumnType("nvarchar(max)");
        builder.HasOne(s => s.Plan)
            .WithMany(p => p.Pasos)
            .HasForeignKey(s => s.IdPlan)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanDependencyConfiguration : IEntityTypeConfiguration<PlanDependency>
{
    public void Configure(EntityTypeBuilder<PlanDependency> builder)
    {
        builder.ToTable("PlanDependency");
        builder.HasKey(d => d.IdDependency);
        builder.HasOne(d => d.Plan)
            .WithMany(p => p.Dependencias)
            .HasForeignKey(d => d.IdPlan)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanExecutionLogConfiguration : IEntityTypeConfiguration<PlanExecutionLog>
{
    public void Configure(EntityTypeBuilder<PlanExecutionLog> builder)
    {
        builder.ToTable("PlanExecutionLog");
        builder.HasKey(l => l.IdLog);
        builder.Property(l => l.Evento).IsRequired();
        builder.Property(l => l.Detalle).HasColumnType("nvarchar(max)");
        builder.HasOne(l => l.Plan)
            .WithMany(p => p.Logs)
            .HasForeignKey(l => l.IdPlan)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
