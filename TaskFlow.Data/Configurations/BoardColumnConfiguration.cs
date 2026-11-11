
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class BoardColumnConfiguration
    : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.HasKey(bc => bc.Id);

        builder.Property(bc => bc.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(bc => bc.Description)
            .HasMaxLength(500);

        builder.Property(bc => bc.Position)
            .IsRequired();

        builder.Property(bc => bc.WipLimit);

        builder.Property(bc => bc.IsDefault)
            .IsRequired();

        builder.Property(bc => bc.IsCompleted)
            .IsRequired();

        builder.Property(bc => bc.CreatedAt)
            .IsRequired();

        // Board - BoardColumn relationship
        builder.HasOne(bc => bc.Board)
            .WithMany(b => b.Columns)
            .HasForeignKey(bc => bc.BoardId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique position within each board
        builder.HasIndex(bc => new { bc.BoardId, bc.Position })
            .IsUnique();

        // Unique column name within each board
        builder.HasIndex(bc => new { bc.BoardId, bc.Name })
            .IsUnique();

        // Only one default column per board
        builder.HasIndex(bc => bc.BoardId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
    }
}
