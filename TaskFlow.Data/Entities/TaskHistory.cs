
using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class TaskHistory
{
    // Primary Key
    public Guid Id { get; set; } = Guid.NewGuid();

    // Related Task
    public Guid TaskId { get; set; }

    // User Who Performed the Action
    // Nullable because some actions may be system-generated
    public string? UserId { get; set; }

    // Action Information
    public TaskHistoryActionType ActionType { get; set; }

    // Changed Field
    public string? FieldName { get; set; }

    // Previous and New Values
    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    // Human-Readable Description
    public string? Description { get; set; }

    // Timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public Task Task { get; set; } = null!;

    public ApplicationUser? User { get; set; }
}
