using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class AIAnalysisItemConfiguration : IEntityTypeConfiguration<AIAnalysisItem>
{
    public void Configure(EntityTypeBuilder<AIAnalysisItem> builder)
    {
        builder.ToTable("AIAnalysisItems", table => table.HasCheckConstraint("CK_AIAnalysisItems_PriorityScore", "[PriorityScore] BETWEEN 0 AND 100"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.SuggestedPriority).IsRequired();
        builder.Property(i => i.PriorityScore).IsRequired();
        builder.Property(i => i.Explanation).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(i => i.RiskLevel).HasMaxLength(50);
        builder.Property(i => i.CreatedAt).IsRequired();

        builder.HasOne(i => i.AIAnalysisSession).WithMany(session => session.Items).HasForeignKey(i => i.AIAnalysisSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Task).WithMany(task => task.AIAnalysisItems).HasForeignKey(i => i.TaskId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.AIAnalysisSessionId, i.TaskId }).IsUnique();
        builder.HasIndex(i => i.TaskId);
    }
}
