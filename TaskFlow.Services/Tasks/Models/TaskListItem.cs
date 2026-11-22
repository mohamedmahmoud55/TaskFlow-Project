using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Tasks.Models;

public class TaskListItem
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public TaskPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public int Position { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsArchived { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    // Whether the current user may edit, archive or delete this task.
    public bool CanEdit { get; set; }
}