using Asistente.Domain.Entities.Aprobaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequest");
        builder.HasKey(a => a.IdApproval);
        builder.Property(a => a.Codigo).IsRequired().HasMaxLength(30);
        builder.Property(a => a.Tipo).HasConversion<int>();
        builder.Property(a => a.Estado).HasConversion<int>();
        builder.Property(a => a.Observaciones).HasColumnType("nvarchar(max)");

        builder.HasOne(a => a.Policy)
            .WithMany()
            .HasForeignKey(a => a.IdPolicy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(a => a.Asignados)
            .WithOne(x => x.Approval)
            .HasForeignKey(x => x.IdApproval)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Decisiones)
            .WithOne(d => d.Approval)
            .HasForeignKey(d => d.IdApproval)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("ApprovalDecision");
        builder.HasKey(d => d.IdDecision);
        builder.Property(d => d.Decision).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Comentario).HasColumnType("nvarchar(max)");
    }
}

public class ApprovalAssigneeConfiguration : IEntityTypeConfiguration<ApprovalAssignee>
{
    public void Configure(EntityTypeBuilder<ApprovalAssignee> builder)
    {
        builder.ToTable("ApprovalAssignee");
        builder.HasKey(a => a.IdAssignee);
        builder.Property(a => a.Estado).IsRequired().HasMaxLength(20).HasDefaultValue("Pendiente");
    }
}

public class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicy> builder)
    {
        builder.ToTable("ApprovalPolicy");
        builder.HasKey(p => p.IdPolicy);
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Activo).IsRequired().HasDefaultValue(true);
    }
}
