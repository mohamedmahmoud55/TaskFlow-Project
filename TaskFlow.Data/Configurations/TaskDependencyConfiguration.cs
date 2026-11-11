
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskDependencyConfiguration
    : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        // Table Name
        builder.ToTable("TaskDependencies");

        // Primary Key
        builder.HasKey(td => td.Id);

        // Dependency Type
        builder.Property(td => td.DependencyType)
            .IsRequired();

        // Lag
        builder.Property(td => td.LagMinutes)
            .IsRequired();

        // Created At
        builder.Property(td => td.CreatedAt)
            .IsRequired();

        // Predecessor Relationship
        builder.HasOne(td => td.PredecessorTask)
            .WithMany(t => t.Successors)
            .HasForeignKey(td => td.PredecessorTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Successor Relationship
        builder.HasOne(td => td.SuccessorTask)
            .WithMany(t => t.Predecessors)
            .HasForeignKey(td => td.SuccessorTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate dependency of the same type
        builder.HasIndex(td => new
        {
            td.PredecessorTaskId,
            td.SuccessorTaskId,
            td.DependencyType
        }).IsUnique();

        // Indexes
        builder.HasIndex(td => td.SuccessorTaskId);
    }
}
