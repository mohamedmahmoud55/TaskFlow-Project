using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class WorkspaceConfiguration
    : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(w => w.Description)
            .HasMaxLength(1000);

        builder.Property(w => w.LogoUrl)
            .HasMaxLength(2048);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.IsArchived)
            .IsRequired();

        builder.HasIndex(w => w.Name);
    }
}