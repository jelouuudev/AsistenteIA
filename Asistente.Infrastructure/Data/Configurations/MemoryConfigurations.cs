using Asistente.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Asistente.Infrastructure.Data.Configurations;

public class MemoryScopeConfiguration : IEntityTypeConfiguration<MemoryScope>
{
    public void Configure(EntityTypeBuilder<MemoryScope> builder)
    {
        builder.ToTable("MemoryScopes");
        builder.HasKey(s => s.IdScope);
        builder.Property(s => s.Codigo).HasMaxLength(30).IsRequired();
        builder.HasIndex(s => s.Codigo).IsUnique();
        builder.Property(s => s.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Descripcion).HasMaxLength(500);
    }
}

public class MemoryCategoryConfiguration : IEntityTypeConfiguration<MemoryCategory>
{
    public void Configure(EntityTypeBuilder<MemoryCategory> builder)
    {
        builder.ToTable("MemoryCategories");
        builder.HasKey(c => c.IdCategory);
        builder.Property(c => c.Codigo).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Codigo).IsUnique();
        builder.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
    }
}

public class MemoryRecordConfiguration : IEntityTypeConfiguration<MemoryRecord>
{
    public void Configure(EntityTypeBuilder<MemoryRecord> builder)
    {
        builder.ToTable("MemoryRecords");
        builder.HasKey(m => m.IdMemory);
        builder.Property(m => m.Codigo).HasMaxLength(100).IsRequired();
        builder.HasIndex(m => m.Codigo).IsUnique();
        builder.Property(m => m.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Contenido).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasOne(m => m.Scope).WithMany(s => s.Memorias)
            .HasForeignKey(m => m.IdScope).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Category).WithMany(c => c.Memorias)
            .HasForeignKey(m => m.IdCategory).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(m => new { m.IdScope, m.Activo });
    }
}

public class MemoryVersionConfiguration : IEntityTypeConfiguration<MemoryVersion>
{
    public void Configure(EntityTypeBuilder<MemoryVersion> builder)
    {
        builder.ToTable("MemoryVersions");
        builder.HasKey(v => v.IdVersion);
        builder.Property(v => v.Contenido).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(v => v.Motivo).HasMaxLength(500);
        builder.HasOne(v => v.Memory).WithMany(m => m.Versiones)
            .HasForeignKey(v => v.IdMemory).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.IdMemory, v.Numero }).IsUnique();
    }
}

public class MemoryAccessLogConfiguration : IEntityTypeConfiguration<MemoryAccessLog>
{
    public void Configure(EntityTypeBuilder<MemoryAccessLog> builder)
    {
        builder.ToTable("MemoryAccessLogs");
        builder.HasKey(l => l.IdLog);
        builder.Property(l => l.Codigo).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Operacion).HasMaxLength(30).IsRequired();
        builder.Property(l => l.Resultado).HasMaxLength(30).IsRequired();
        builder.Property(l => l.Motivo).HasMaxLength(1000);
        builder.HasIndex(l => new { l.IdMemory, l.Fecha });
        builder.HasIndex(l => new { l.Codigo, l.Fecha });
    }
}

public class MemoryRetentionPolicyConfiguration : IEntityTypeConfiguration<MemoryRetentionPolicy>
{
    public void Configure(EntityTypeBuilder<MemoryRetentionPolicy> builder)
    {
        builder.ToTable("MemoryRetentionPolicies");
        builder.HasKey(p => p.IdPolicy);
        builder.HasOne(p => p.Scope).WithMany(s => s.Politicas)
            .HasForeignKey(p => p.IdScope).OnDelete(DeleteBehavior.SetNull);
    }
}
