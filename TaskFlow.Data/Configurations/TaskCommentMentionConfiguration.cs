using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class TaskCommentMentionConfiguration : IEntityTypeConfiguration<TaskCommentMention>
{
    public void Configure(EntityTypeBuilder<TaskCommentMention> builder)
    {
        builder.ToTable("TaskCommentMentions");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MentionedUserId).IsRequired();
        builder.Property(m => m.MentionedAt).IsRequired();

        builder.HasOne(m => m.TaskComment)
            .WithMany(comment => comment.Mentions)
            .HasForeignKey(m => m.TaskCommentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.MentionedUser)
            .WithMany(user => user.Mentions)
            .HasForeignKey(m => m.MentionedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.TaskCommentId, m.MentionedUserId }).IsUnique();
        builder.HasIndex(m => m.MentionedUserId);
    }
}
