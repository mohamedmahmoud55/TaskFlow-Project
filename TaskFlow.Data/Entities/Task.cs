
using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class Task
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Keys
    public Guid BoardColumnId { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    // Task Information
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    // Scheduling
    public DateTime? StartDate { get; set; }

    public DateTime? DueDate { get; set; }

    public int? EstimatedMinutes { get; set; }

    // Ordering
    public int Position { get; set; }

    // Tracking
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsArchived { get; set; } = false;

    // Navigation Properties
    public BoardColumn BoardColumn { get; set; } = null!;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ICollection<TaskAssignee> Assignees { get; set; }
        = new List<TaskAssignee>();

    public ICollection<Subtask> Subtasks { get; set; }
        = new List<Subtask>();

    public ICollection<TaskLabel> TaskLabels { get; set; }
        = new List<TaskLabel>();

    // Tasks that this Task depends on
    public ICollection<TaskDependency> Predecessors { get; set; }
        = new List<TaskDependency>();

    // Tasks that depend on this Task
    public ICollection<TaskDependency> Successors { get; set; }
        = new List<TaskDependency>();

    public ICollection<TaskComment> Comments { get; set; }
        = new List<TaskComment>();
    public ICollection<Attachment> Attachments { get; set; }
    = new List<Attachment>();
    // Checklist Items
    public ICollection<ChecklistItem> ChecklistItems { get; set; }
        = new List<ChecklistItem>();
    // Task History
    public ICollection<TaskHistory> History { get; set; }
        = new List<TaskHistory>();
    // Time Entries
    public ICollection<TimeEntry> TimeEntries { get; set; }
        = new List<TimeEntry>();

    public ICollection<TaskTimer> Timers { get; set; }
        = new List<TaskTimer>();

    public ICollection<TaskAIRecommendation> AIRecommendations { get; set; }
        = new List<TaskAIRecommendation>();

    public ICollection<AIAnalysisItem> AIAnalysisItems { get; set; }
        = new List<AIAnalysisItem>();

    public ICollection<CustomFieldValue> CustomFieldValues { get; set; }
        = new List<CustomFieldValue>();
}
