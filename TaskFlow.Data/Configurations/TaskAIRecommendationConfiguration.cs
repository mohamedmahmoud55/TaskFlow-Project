using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskAIRecommendationConfiguration : IEntityTypeConfiguration<TaskAIRecommendation>
{
    public void Configure(EntityTypeBuilder<TaskAIRecommendation> builder)
    {
        builder.ToTable("TaskAIRecommendations", table => table.HasCheckConstraint("CK_TaskAIRecommendations_PriorityScore", "[PriorityScore] BETWEEN 0 AND 100"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RequestedByUserId).IsRequired();
        builder.Property(r => r.SuggestedPriority).IsRequired();
        builder.Property(r => r.PriorityScore).IsRequired();
        builder.Property(r => r.Explanation).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(r => r.ModelProvider).IsRequired().HasMaxLength(100);
        builder.Property(r => r.ModelName).HasMaxLength(200);
        builder.Property(r => r.IsApproved);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.Task).WithMany(task => task.AIRecommendations).HasForeignKey(r => r.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.RequestedByUser).WithMany(user => user.AIRecommendationsRequested).HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ApprovedByUser).WithMany(user => user.AIRecommendationsApproved).HasForeignKey(r => r.ApprovedByUserId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.TaskId, r.CreatedAt });
        builder.HasIndex(r => new { r.TaskId, r.IsApproved });
        builder.HasIndex(r => r.RequestedByUserId);
    }
}
