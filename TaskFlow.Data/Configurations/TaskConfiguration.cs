
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Data.Configurations;

public class TaskConfiguration : IEntityTypeConfiguration<TaskEntity>
{
    public void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        builder.HasKey(t => t.Id);

        // Task Information
        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(5000);

        builder.Property(t => t.Priority)
            .IsRequired();

        // Scheduling
        builder.Property(t => t.EstimatedMinutes);

        // Tracking
        builder.Property(t => t.Position)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.IsArchived)
            .IsRequired();

        // BoardColumn - Task relationship
        builder.HasOne(t => t.BoardColumn)
            .WithMany(bc => bc.Tasks)
            .HasForeignKey(t => t.BoardColumnId)
            .OnDelete(DeleteBehavior.Restrict);

        // ApplicationUser - CreatedTasks relationship
        builder.HasOne(t => t.CreatedByUser)
            .WithMany(u => u.CreatedTasks)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique task position within each column
        builder.HasIndex(t => new { t.BoardColumnId, t.Position })
            .IsUnique();

        // Useful indexes for searching and filtering
        builder.HasIndex(t => t.CreatedByUserId);

        builder.HasIndex(t => t.DueDate);
    }
}
