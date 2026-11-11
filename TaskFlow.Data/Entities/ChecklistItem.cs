
namespace TaskFlow.Data.Entities;

public class ChecklistItem
{
    // Primary Key
    public Guid Id { get; set; } = Guid.NewGuid();

    // Parent Task
    public Guid TaskId { get; set; }

    // Checklist Information
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public int Position { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation Property
    public Task Task { get; set; } = null!;
}
