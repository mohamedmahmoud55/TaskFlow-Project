using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskAssigneeConfiguration
    : IEntityTypeConfiguration<TaskAssignee>
{
    public void Configure(EntityTypeBuilder<TaskAssignee> builder)
    {
       

        // Table Name
        builder.ToTable("TaskAssignees");
        // Primary Key
        builder.HasKey(ta => ta.Id);


        // Assigned User
        builder.Property(ta => ta.UserId)
            .IsRequired();

        // Assigned At
        builder.Property(ta => ta.AssignedAt)
            .IsRequired();

        // Task Relationship
        builder.HasOne(ta => ta.Task)
            .WithMany(t => t.Assignees)
            .HasForeignKey(ta => ta.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // User Relationship
        builder.HasOne(ta => ta.User)
            .WithMany(u => u.AssignedTasks)
            .HasForeignKey(ta => ta.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Assigned By Relationship
        builder.HasOne(ta => ta.AssignedByUser)
            .WithMany(u => u.AssignedTasksByMe)
            .HasForeignKey(ta => ta.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate assignments
        builder.HasIndex(ta => new { ta.TaskId, ta.UserId })
            .IsUnique();

        // Indexes
        builder.HasIndex(ta => ta.UserId);
        builder.HasIndex(ta => ta.AssignedAt);
    }
}