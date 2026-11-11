
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TimeEntryConfiguration
    : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        // Table Name
        builder.ToTable("TimeEntries");

        // Primary Key
        builder.HasKey(t => t.Id);

        // Description
        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        // Start Time
        builder.Property(t => t.StartTime)
            .IsRequired();

        // End Time
        builder.Property(t => t.EndTime)
            .IsRequired();

        // Duration
        builder.Property(t => t.DurationMinutes)
            .IsRequired();

        // Created At
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // Task Relationship
        builder.HasOne(t => t.Task)
            .WithMany(task => task.TimeEntries)
            .HasForeignKey(t => t.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // User Relationship
        builder.HasOne(t => t.User)
            .WithMany(user => user.TimeEntries)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(t => t.TaskId);
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.StartTime);
    }
}
