using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.ToTable("ProjectMilestones");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Description).HasMaxLength(2000);
        builder.Property(m => m.Status).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.HasOne(m => m.Project).WithMany().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => new { m.ProjectId, m.DueDate });
        builder.HasIndex(m => new { m.ProjectId, m.Status });
    }
}
