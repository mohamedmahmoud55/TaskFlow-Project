
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskLabelConfiguration
    : IEntityTypeConfiguration<TaskLabel>
{
    public void Configure(EntityTypeBuilder<TaskLabel> builder)
    {
        // Table Name
        builder.ToTable("TaskLabels");

        // Primary Key
        builder.HasKey(tl => tl.Id);

        // Assigned At
        builder.Property(tl => tl.AssignedAt)
            .IsRequired();

        // Task Relationship
        builder.HasOne(tl => tl.Task)
            .WithMany(t => t.TaskLabels)
            .HasForeignKey(tl => tl.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Label Relationship
        builder.HasOne(tl => tl.Label)
            .WithMany(l => l.TaskLabels)
            .HasForeignKey(tl => tl.LabelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate labels on the same task
        builder.HasIndex(tl => new { tl.TaskId, tl.LabelId })
            .IsUnique();

        // Additional indexes
        builder.HasIndex(tl => tl.LabelId);
        builder.HasIndex(tl => tl.AssignedAt);
    }
}
