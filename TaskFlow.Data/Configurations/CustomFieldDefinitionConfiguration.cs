using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("CustomFieldDefinitions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);
        builder.Property(d => d.FieldType).IsRequired();
        builder.Property(d => d.OptionsJson).HasColumnType("nvarchar(max)");
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.HasOne(d => d.Workspace).WithMany().HasForeignKey(d => d.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Project).WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.WorkspaceId, d.ProjectId, d.Name });
        builder.HasIndex(d => d.ProjectId);
    }
}
