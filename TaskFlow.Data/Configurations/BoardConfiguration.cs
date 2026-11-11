
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(b => b.Description)
            .HasMaxLength(1000);

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.IsArchived)
            .IsRequired();

        builder.Property(b => b.IsDefault)
            .IsRequired();

        // Project - Board relationship
        builder.HasOne(b => b.Project)
            .WithMany(p => p.Boards)
            .HasForeignKey(b => b.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only one default board per project
        builder.HasIndex(b => b.ProjectId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1");

        // Board names must be unique within a project
        builder.HasIndex(b => new { b.ProjectId, b.Name })
            .IsUnique();
    }
}
