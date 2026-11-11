using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
