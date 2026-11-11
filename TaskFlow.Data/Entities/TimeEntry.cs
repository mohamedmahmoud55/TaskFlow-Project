
namespace TaskFlow.Data.Entities;

public class TimeEntry
{
    // Primary Key
    public Guid Id { get; set; } = Guid.NewGuid();

    // Related Task
    public Guid TaskId { get; set; }

    // User Who Worked
    public string UserId { get; set; } = string.Empty;

    // Work Description
    public string? Description { get; set; }

    // Time Information
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int DurationMinutes { get; set; }

    // Creation Timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public Task Task { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
