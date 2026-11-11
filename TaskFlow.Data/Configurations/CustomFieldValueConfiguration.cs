using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class CustomFieldValueConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        builder.ToTable("CustomFieldValues");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.TextValue).HasMaxLength(4000);
        builder.Property(v => v.NumberValue).HasColumnType("decimal(18,4)");
        builder.Property(v => v.SelectedValue).HasMaxLength(500);
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.HasOne(v => v.CustomFieldDefinition).WithMany(d => d.Values).HasForeignKey(v => v.CustomFieldDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.Task).WithMany(task => task.CustomFieldValues).HasForeignKey(v => v.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.CustomFieldDefinitionId, v.TaskId }).IsUnique();
        builder.HasIndex(v => v.TaskId);
    }
}
