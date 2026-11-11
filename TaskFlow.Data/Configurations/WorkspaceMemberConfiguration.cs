
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class WorkspaceMemberConfiguration
    : IEntityTypeConfiguration<WorkspaceMember>
{
    void IEntityTypeConfiguration<WorkspaceMember>
        .Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        // Primary Key
        builder.HasKey(wm => wm.Id);

        // Workspace Relationship
        builder.HasOne(wm => wm.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(wm => wm.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // User Relationship
        builder.HasOne(wm => wm.User)
            .WithMany(u => u.WorkspaceMemberships)
            .HasForeignKey(wm => wm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique Membership
        builder.HasIndex(wm => new { wm.WorkspaceId, wm.UserId })
            .IsUnique();

        // Required Properties
        builder.Property(wm => wm.UserId)
            .IsRequired();

        builder.Property(wm => wm.Role)
            .IsRequired();

        builder.Property(wm => wm.JoinedAt)
            .IsRequired();

        builder.Property(wm => wm.IsActive)
            .IsRequired();
    }
}
