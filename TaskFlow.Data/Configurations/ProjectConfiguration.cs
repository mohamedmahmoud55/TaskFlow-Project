
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class ProjectConfiguration
    : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.IsArchived)
            .IsRequired();

        // Team - Project relationship
        builder.HasOne(p => p.Team)
            .WithMany(t => t.Projects)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Search projects within a team
        builder.HasIndex(p => new { p.TeamId, p.Name });
    }
}
