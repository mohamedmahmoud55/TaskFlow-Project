namespace TaskFlow.Data.Entities;

public class TaskAssignee
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; }
    public string? AssignedByUserId { get; set; }

    // Navigation Properties
    public Task Task { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser? AssignedByUser { get; set; }
}