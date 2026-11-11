
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class ChecklistItemConfiguration
    : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        // Table Name
        builder.ToTable("ChecklistItems");

        // Primary Key
        builder.HasKey(c => c.Id);

        // Title
        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(300);

        // Completion Status
        builder.Property(c => c.IsCompleted)
            .IsRequired();

        // Position
        builder.Property(c => c.Position)
            .IsRequired();

        // Timestamps
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Task Relationship
        builder.HasOne(c => c.Task)
            .WithMany(t => t.ChecklistItems)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => new { c.TaskId, c.Position })
            .IsUnique();

        builder.HasIndex(c => c.IsCompleted);
    }
}
