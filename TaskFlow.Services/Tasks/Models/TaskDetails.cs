using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Tasks.Models;

public class TaskDetails
{
    public Guid Id { get; set; }

    public Guid BoardColumnId { get; set; }

    public Guid BoardId { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskPriority Priority { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? DueDate { get; set; }

    public int? EstimatedMinutes { get; set; }

    public int Position { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsArchived { get; set; }

    public string ColumnName { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}