using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskTimerConfiguration : IEntityTypeConfiguration<TaskTimer>
{
    public void Configure(EntityTypeBuilder<TaskTimer> builder)
    {
        builder.ToTable("TaskTimers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.StartedAt).IsRequired();
        builder.Property(t => t.TotalPausedSeconds).IsRequired();
        builder.Property(t => t.IsRunning).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasOne(t => t.Task)
            .WithMany(task => task.Timers)
            .HasForeignKey(t => t.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.User)
            .WithMany(user => user.TaskTimers)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.UserId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
        builder.HasIndex(t => new { t.TaskId, t.IsActive });
    }
}
