using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("WorkspaceInvitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.InvitedEmail).IsRequired().HasMaxLength(320);
        builder.Property(i => i.InvitedRole).IsRequired();
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.HasOne(i => i.Workspace).WithMany().HasForeignKey(i => i.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.InvitedByUser).WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.WorkspaceId, i.InvitedEmail, i.Status });
        builder.HasIndex(i => i.ExpiresAt);
    }
}
