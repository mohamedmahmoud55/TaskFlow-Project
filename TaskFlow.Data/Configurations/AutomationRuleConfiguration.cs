using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("AutomationRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.CreatedByUserId).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.TriggerType).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ConditionsJson).HasColumnType("nvarchar(max)");
        builder.Property(r => r.ActionsJson).HasColumnType("nvarchar(max)");
        builder.Property(r => r.IsEnabled).IsRequired();
        builder.Property(r => r.ExecutionCount).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.Workspace).WithMany().HasForeignKey(r => r.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CreatedByUser).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.WorkspaceId, r.IsEnabled });
        builder.HasIndex(r => new { r.WorkspaceId, r.TriggerType });
        builder.HasIndex(r => r.CreatedByUserId);
    }
}
