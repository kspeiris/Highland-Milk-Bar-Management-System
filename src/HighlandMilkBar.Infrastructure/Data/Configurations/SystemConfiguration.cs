using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighlandMilkBar.Infrastructure.Data.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.Property(x => x.Key).IsRequired().HasMaxLength(120);
        b.Property(x => x.Value).HasMaxLength(1000);
        b.HasIndex(x => x.Key).IsUnique();
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Action).IsRequired().HasMaxLength(120);
        b.Property(x => x.EntityType).HasMaxLength(120);
        b.Property(x => x.Details).HasMaxLength(1000);
        b.HasIndex(x => x.Timestamp);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}