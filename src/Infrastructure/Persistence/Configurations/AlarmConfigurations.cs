using Domain.Entities.Alarms;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class AlarmRuleConfiguration : IEntityTypeConfiguration<AlarmRule>
{
    public void Configure(EntityTypeBuilder<AlarmRule> builder)
    {
        builder.ToTable("AlarmRules", SchemaNames.Alarm);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RuleType).HasConversion(new EnumStringValueConverter<AlarmRuleType>()).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ScopeType).HasConversion(new EnumStringValueConverter<AlarmScopeType>()).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TagName).HasMaxLength(100);
        builder.Property(x => x.MinimumValue).HasPrecision(14, 4);
        builder.Property(x => x.MaximumValue).HasPrecision(14, 4);
        builder.Property(x => x.Severity).HasConversion(new EnumStringValueConverter<AlarmSeverity>()).HasMaxLength(15).IsRequired();
        builder.Property(x => x.WorkOrderTeam).HasMaxLength(60);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.IsActive });
    }
}

public class AlarmRuleScopeConfiguration : IEntityTypeConfiguration<AlarmRuleScope>
{
    public void Configure(EntityTypeBuilder<AlarmRuleScope> builder)
    {
        builder.ToTable("AlarmRuleScopes", SchemaNames.Alarm);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.ScopeType).HasConversion(new EnumStringValueConverter<AlarmScopeType>()).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.AlarmRule).WithMany(x => x.Scopes).HasForeignKey(x => x.AlarmRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AlarmRuleId, x.ScopeType, x.EntityId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AlarmRuleActionConfiguration : IEntityTypeConfiguration<AlarmRuleAction>
{
    public void Configure(EntityTypeBuilder<AlarmRuleAction> builder)
    {
        builder.ToTable("AlarmRuleActions", SchemaNames.Alarm);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.ActionType).HasConversion(new EnumStringValueConverter<AlarmRuleActionType>()).HasMaxLength(30).IsRequired();
        builder.Property(x => x.WorkOrderPriority).HasConversion(new EnumStringValueConverter<WorkOrderPriority>()).HasMaxLength(15);
        builder.HasOne(x => x.AlarmRule).WithMany(x => x.Actions).HasForeignKey(x => x.AlarmRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AlarmRuleId, x.SequenceNo }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AlarmRuleEmailTargetConfiguration : IEntityTypeConfiguration<AlarmRuleEmailTarget>
{
    public void Configure(EntityTypeBuilder<AlarmRuleEmailTarget> builder)
    {
        builder.ToTable("AlarmRuleEmailTargets", SchemaNames.Alarm);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.AlarmRule).WithMany(x => x.EmailTargets).HasForeignKey(x => x.AlarmRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AlarmRuleId, x.Email }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AlarmRuleSmsTargetConfiguration : IEntityTypeConfiguration<AlarmRuleSmsTarget>
{
    public void Configure(EntityTypeBuilder<AlarmRuleSmsTarget> builder)
    {
        builder.ToTable("AlarmRuleSmsTargets", SchemaNames.Alarm);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        builder.HasOne(x => x.AlarmRule).WithMany(x => x.SmsTargets).HasForeignKey(x => x.AlarmRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.AlarmRuleId, x.PhoneNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class AlarmEventConfiguration : IEntityTypeConfiguration<AlarmEvent>
{
    public void Configure(EntityTypeBuilder<AlarmEvent> builder)
    {
        builder.ToTable("AlarmEvents", SchemaNames.Alarm);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Severity).HasConversion(new EnumStringValueConverter<AlarmSeverity>()).HasMaxLength(15).IsRequired();
        builder.Property(x => x.TagName).HasMaxLength(100);
        builder.Property(x => x.TriggerValue).HasPrecision(14, 4);
        builder.Property(x => x.TriggeredAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ResolvedAt).HasColumnType("datetime2");
        builder.Property(x => x.AcknowledgedAt).HasColumnType("datetime2");
        builder.Property(x => x.ProbableCause).HasColumnType("nvarchar(max)");
        builder.HasOne(x => x.Rule).WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Silo).WithMany().HasForeignKey(x => x.SiloId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.TriggeredAt });
        builder.HasIndex(x => new { x.DeviceId, x.ResolvedAt }).HasFilter("[ResolvedAt] IS NULL");
    }
}
