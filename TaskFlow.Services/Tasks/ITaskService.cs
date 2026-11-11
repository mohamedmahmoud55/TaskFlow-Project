using TaskFlow.Data.Enums;
using TaskFlow.Services.Tasks.Models;

namespace TaskFlow.Services.Tasks;

public interface ITaskService
{
    // Task queries
    Task<IReadOnlyList<TaskListItem>> GetTasksAsync(
        Guid boardColumnId,
        string userId);

    Task<TaskDetails?> GetTaskAsync(
        Guid taskId,
        Guid? expectedBoardColumnId,
        string userId);

    // Task creation
    Task<TaskCreationResult> CreateTaskAsync(
        Guid boardColumnId,
        string userId,
        string title,
        string? description,
        TaskPriority priority,
        DateTime? startDate,
        DateTime? dueDate,
        int? estimatedMinutes);

    // Task updates
    Task<bool> UpdateTaskAsync(
        Guid taskId,
        string userId,
        string title,
        string? description,
        TaskPriority priority,
        DateTime? startDate,
        DateTime? dueDate,
        int? estimatedMinutes);

    // Task deletion and archiving
    Task<bool> ArchiveTaskAsync(
        Guid taskId,
        string userId);

    Task<bool> DeleteTaskAsync(
        Guid taskId,
        string userId);
}