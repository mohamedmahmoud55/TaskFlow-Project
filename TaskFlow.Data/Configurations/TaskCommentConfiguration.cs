
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskCommentConfiguration
    : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        // Table Name
        builder.ToTable("TaskComments");

        // Primary Key
        builder.HasKey(c => c.Id);

        // Comment Content
        builder.Property(c => c.Content)
    .IsRequired()
    .HasMaxLength(5000)
    .HasColumnType("nvarchar(max)");
        // Created At
        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Is Deleted
        builder.Property(c => c.IsDeleted)
            .IsRequired();

        // Task Relationship
        builder.HasOne(c => c.Task)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comment Author
        builder.HasOne(c => c.User)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Parent Comment / Replies
        builder.HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // User Who Deleted the Comment
        builder.HasOne(c => c.DeletedByUser)
            .WithMany(u => u.DeletedComments)
            .HasForeignKey(c => c.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(c => c.TaskId);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.ParentCommentId);
        builder.HasIndex(c => c.CreatedAt);
    }
}
