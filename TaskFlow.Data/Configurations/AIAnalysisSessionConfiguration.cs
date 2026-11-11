using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class AIAnalysisSessionConfiguration : IEntityTypeConfiguration<AIAnalysisSession>
{
    public void Configure(EntityTypeBuilder<AIAnalysisSession> builder)
    {
        builder.ToTable("AIAnalysisSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.RequestedByUserId).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.ModelProvider).IsRequired().HasMaxLength(100);
        builder.Property(s => s.ModelName).HasMaxLength(200);
        builder.Property(s => s.InputSummary).HasColumnType("nvarchar(max)");
        builder.Property(s => s.ResultSummary).HasColumnType("nvarchar(max)");
        builder.Property(s => s.ErrorMessage).HasColumnType("nvarchar(max)");
        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasOne(s => s.Workspace).WithMany(workspace => workspace.AIAnalysisSessions).HasForeignKey(s => s.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.RequestedByUser).WithMany(user => user.AIAnalysisSessions).HasForeignKey(s => s.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.WorkspaceId, s.CreatedAt });
        builder.HasIndex(s => new { s.RequestedByUserId, s.CreatedAt });
        builder.HasIndex(s => s.Status);
    }
}
