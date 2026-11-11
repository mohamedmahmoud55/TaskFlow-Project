
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.IsArchived)
            .IsRequired();

        // Workspace - Team relationship
        builder.HasOne(t => t.Workspace)
            .WithMany(w => w.Teams)
            .HasForeignKey(t => t.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for searching teams by workspace and name
        builder.HasIndex(t => new { t.WorkspaceId, t.Name });
    }
}
