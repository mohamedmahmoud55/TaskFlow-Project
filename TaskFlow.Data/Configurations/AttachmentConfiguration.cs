
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class AttachmentConfiguration
    : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        // Table Name
        builder.ToTable("Attachments");

        // Primary Key
        builder.HasKey(a => a.Id);

        // Original File Name
        builder.Property(a => a.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        // Stored File Name
        builder.Property(a => a.StoredFileName)
            .IsRequired()
            .HasMaxLength(255);

        // Storage Path
        builder.Property(a => a.StoragePath)
            .IsRequired()
            .HasMaxLength(2048);

        // Content Type
        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(255);

        // File Size
        builder.Property(a => a.FileSizeBytes)
            .IsRequired();

        // Uploaded At
        builder.Property(a => a.UploadedAt)
            .IsRequired();

        // Task Relationship
        builder.HasOne(a => a.Task)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional Comment Relationship
        builder.HasOne(a => a.TaskComment)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.TaskCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Uploader Relationship
        builder.HasOne(a => a.UploadedByUser)
            .WithMany(u => u.UploadedAttachments)
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(a => a.TaskId);
        builder.HasIndex(a => a.TaskCommentId);
        builder.HasIndex(a => a.UploadedByUserId);
        builder.HasIndex(a => a.UploadedAt);
    }
}
