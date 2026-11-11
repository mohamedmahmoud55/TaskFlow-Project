namespace TaskFlow.Data.Entities;

public class TaskCommentMention
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskCommentId { get; set; }
    public string MentionedUserId { get; set; } = string.Empty;
    public DateTime MentionedAt { get; set; } = DateTime.UtcNow;

    public TaskComment TaskComment { get; set; } = null!;
    public ApplicationUser MentionedUser { get; set; } = null!;
}
