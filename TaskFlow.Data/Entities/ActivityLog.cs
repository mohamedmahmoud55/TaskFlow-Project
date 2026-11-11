using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? WorkspaceId { get; set; }
    public string? UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public ActivityActionType ActionType { get; set; }
    public string? Description { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Workspace? Workspace { get; set; }
    public ApplicationUser? User { get; set; }
}
