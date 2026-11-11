
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class ProjectMemberConfiguration
    : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasKey(pm => pm.Id);

        builder.HasOne(pm => pm.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(pm => pm.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pm => pm.User)
            .WithMany(u => u.ProjectMemberships)
            .HasForeignKey(pm => pm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pm => new { pm.ProjectId, pm.UserId })
            .IsUnique();

        builder.Property(pm => pm.UserId).IsRequired();
        builder.Property(pm => pm.Role).IsRequired();
        builder.Property(pm => pm.JoinedAt).IsRequired();
        builder.Property(pm => pm.IsActive).IsRequired();
    }
}
