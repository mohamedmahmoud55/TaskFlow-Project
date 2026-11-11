namespace TaskFlow.Data.Entities;

public class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string? ConditionsJson { get; set; }
    public string? ActionsJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int ExecutionCount { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
}
