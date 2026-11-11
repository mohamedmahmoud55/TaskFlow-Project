using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.ActionType).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.OldValue).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValue).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(1000);
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasOne(a => a.Workspace)
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(a => a.User)
            .WithMany(user => user.ActivityLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => new { a.WorkspaceId, a.CreatedAt });
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.CreatedAt);
    }
}
