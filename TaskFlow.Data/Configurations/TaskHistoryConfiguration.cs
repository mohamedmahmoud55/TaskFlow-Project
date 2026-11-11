
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskHistoryConfiguration
    : IEntityTypeConfiguration<TaskHistory>
{
    public void Configure(EntityTypeBuilder<TaskHistory> builder)
    {
        // Table Name
        builder.ToTable("TaskHistories");

        // Primary Key
        builder.HasKey(h => h.Id);

        // Action Type
        builder.Property(h => h.ActionType)
            .IsRequired();

        // Field Name
        builder.Property(h => h.FieldName)
            .HasMaxLength(100);

        // Old Value
        builder.Property(h => h.OldValue)
            .HasMaxLength(5000)
            .HasColumnType("nvarchar(max)");

        // New Value
        builder.Property(h => h.NewValue)
            .HasMaxLength(5000)
            .HasColumnType("nvarchar(max)");

        // Description
        builder.Property(h => h.Description)
            .HasMaxLength(1000);

        // Created At
        builder.Property(h => h.CreatedAt)
            .IsRequired();

        // Task Relationship
        builder.HasOne(h => h.Task)
            .WithMany(t => t.History)
            .HasForeignKey(h => h.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional User Relationship
        builder.HasOne(h => h.User)
            .WithMany(u => u.TaskHistories)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(h => h.TaskId);
        builder.HasIndex(h => h.UserId);
        builder.HasIndex(h => h.CreatedAt);
        builder.HasIndex(h => h.ActionType);
    }
}
