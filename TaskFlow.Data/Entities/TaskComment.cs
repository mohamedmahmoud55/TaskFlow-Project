
namespace TaskFlow.Data.Entities;

public class TaskComment
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public Guid? ParentCommentId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedByUserId { get; set; }

    // Navigation Properties
    public Task Task { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;

    public TaskComment? ParentComment { get; set; }

    public ICollection<TaskComment> Replies { get; set; }
        = new List<TaskComment>();

    public ApplicationUser? DeletedByUser { get; set; }

    public ICollection<Attachment> Attachments { get; set; }
    = new List<Attachment>();

    public ICollection<TaskCommentMention> Mentions { get; set; }
        = new List<TaskCommentMention>();
}
