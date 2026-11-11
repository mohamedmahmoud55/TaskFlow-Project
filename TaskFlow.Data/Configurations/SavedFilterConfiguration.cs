using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> builder)
    {
        builder.ToTable("SavedFilters");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.UserId).IsRequired();
        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Description).HasMaxLength(2000);
        builder.Property(f => f.FilterDefinitionJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(f => f.IsDefault).IsRequired();
        builder.Property(f => f.IsShared).IsRequired();
        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasOne(f => f.User).WithMany(user => user.SavedFilters).HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Workspace).WithMany().HasForeignKey(f => f.WorkspaceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(f => f.Project).WithMany().HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => new { f.UserId, f.WorkspaceId, f.ProjectId, f.Name });
        builder.HasIndex(f => new { f.UserId, f.IsDefault });
        builder.HasIndex(f => f.WorkspaceId);
        builder.HasIndex(f => f.ProjectId);
    }
}
