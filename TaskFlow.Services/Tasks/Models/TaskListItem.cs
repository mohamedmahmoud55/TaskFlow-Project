using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Tasks.Models;

public sealed record TaskAssigneeBadge(
    string UserId,
    string DisplayName,
    string Initials);

public class TaskListItem
{
    public Guid Id { get; set; }

    public Guid BoardColumnId { get; set; }

    public string Title { get; set; } = string.Empty;

    public TaskPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public int Position { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsArchived { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    // Whether the current user may edit or archive this task.
    public bool CanEdit { get; set; }

    public IReadOnlyList<TaskAssigneeBadge> Assignees { get; set; }
        = Array.Empty<TaskAssigneeBadge>();
}
