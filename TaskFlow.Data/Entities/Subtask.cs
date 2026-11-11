
namespace TaskFlow.Data.Entities;

public class Subtask
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsCompleted { get; set; }

    public int Position { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // Navigation Property
    public Task Task { get; set; } = null!;
}
