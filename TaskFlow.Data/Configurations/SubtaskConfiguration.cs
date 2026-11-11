
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class SubtaskConfiguration
    : IEntityTypeConfiguration<Subtask>
{
    public void Configure(EntityTypeBuilder<Subtask> builder)
    {
        // Table Name
        builder.ToTable("Subtasks");

        // Primary Key
        builder.HasKey(s => s.Id);

        // Title
        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        // Description
        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        // Required Properties
        builder.Property(s => s.IsCompleted)
            .IsRequired();

        builder.Property(s => s.Position)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // Task Relationship
        builder.HasOne(s => s.Task)
            .WithMany(t => t.Subtasks)
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent duplicate positions within the same Task
        builder.HasIndex(s => new { s.TaskId, s.Position })
            .IsUnique();

        // Additional Index
        builder.HasIndex(s => s.DueDate);
    }
}
