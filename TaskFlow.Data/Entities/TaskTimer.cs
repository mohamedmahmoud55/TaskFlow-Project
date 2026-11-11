namespace TaskFlow.Data.Entities;

public class TaskTimer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PausedAt { get; set; }
    public int TotalPausedSeconds { get; set; }
    public bool IsRunning { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Task Task { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
