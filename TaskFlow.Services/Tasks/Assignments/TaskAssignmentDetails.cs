namespace TaskFlow.Services.Tasks.Assignments.Models;

public class TaskAssignmentDetails
{
    public Guid TaskId { get; set; }

    public bool CanManageAssignments { get; set; }

    public IReadOnlyList<TaskAssignmentMember> Members { get; set; }
        = Array.Empty<TaskAssignmentMember>();
}