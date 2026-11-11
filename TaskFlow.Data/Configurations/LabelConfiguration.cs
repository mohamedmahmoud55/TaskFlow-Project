
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("Labels");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.Description)
            .HasMaxLength(500);

        builder.Property(l => l.Color)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(l => l.CreatedAt)
            .IsRequired();

        // Workspace Relationship
        builder.HasOne(l => l.Workspace)
            .WithMany(w => w.Labels)
            .HasForeignKey(l => l.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique label name within each Workspace
        builder.HasIndex(l => new { l.WorkspaceId, l.Name })
            .IsUnique();
    }
}
