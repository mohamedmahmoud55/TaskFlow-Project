using TaskFlow.Services.Tasks.Assignments.Models;

namespace TaskFlow.Services.Tasks.Assignments;

public interface ITaskAssignmentService
{
    Task<TaskAssignmentDetails?> GetAssignmentDetailsAsync(
        Guid taskId,
        string userId);

    Task<bool> UpdateAssigneesAsync(
        Guid taskId,
        string userId,
        IReadOnlyCollection<string> assigneeUserIds);
}