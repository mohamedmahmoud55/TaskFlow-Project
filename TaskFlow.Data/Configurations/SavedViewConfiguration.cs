using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("SavedViews");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Description).HasMaxLength(2000);
        builder.Property(v => v.ViewType).IsRequired();
        builder.Property(v => v.ConfigurationJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(v => v.IsDefault).IsRequired();
        builder.Property(v => v.IsShared).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.HasOne(v => v.User).WithMany(user => user.SavedViews).HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Workspace).WithMany().HasForeignKey(v => v.WorkspaceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(v => v.Project).WithMany().HasForeignKey(v => v.ProjectId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(v => new { v.UserId, v.WorkspaceId, v.ProjectId, v.Name });
        builder.HasIndex(v => new { v.UserId, v.IsDefault });
        builder.HasIndex(v => v.WorkspaceId);
        builder.HasIndex(v => v.ProjectId);
    }
}
