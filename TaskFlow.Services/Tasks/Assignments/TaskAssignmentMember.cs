namespace TaskFlow.Services.Tasks.Assignments.Models;

public class TaskAssignmentMember
{
    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsAssigned { get; set; }
}