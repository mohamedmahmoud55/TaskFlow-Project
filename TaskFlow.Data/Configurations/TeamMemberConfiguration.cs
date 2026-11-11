
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TeamMemberConfiguration
    : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.HasKey(tm => tm.Id);

        builder.HasOne(tm => tm.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(tm => tm.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tm => tm.User)
            .WithMany(u => u.TeamMemberships)
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tm => new { tm.TeamId, tm.UserId })
            .IsUnique();

        builder.Property(tm => tm.UserId).IsRequired();
        builder.Property(tm => tm.Role).IsRequired();
        builder.Property(tm => tm.JoinedAt).IsRequired();
        builder.Property(tm => tm.IsActive).IsRequired();
    }
}
