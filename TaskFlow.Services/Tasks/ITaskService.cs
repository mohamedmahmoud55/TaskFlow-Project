using TaskFlow.Data.Enums;
using TaskFlow.Services.Tasks.Models;

namespace TaskFlow.Services.Tasks;

public interface ITaskService
{
    // Task queries
    Task<IReadOnlyList<TaskListItem>> GetTasksAsync(
        Guid boardColumnId,
        string userId);

    // Loads every active task across all columns of a board in one query,
    // ordered by column position then task position.
    Task<IReadOnlyList<TaskListItem>> GetBoardTasksAsync(
        Guid boardId,
        string userId);

    Task<TaskDetails?> GetTaskAsync(
        Guid taskId,
        Guid? expectedBoardColumnId,
        string userId);

    // Whether the user may add tasks to the column.
    Task<bool> CanCreateTasksAsync(
        Guid boardColumnId,
        string userId);

    // Where a column sits (project and board), for navigation. Null when
    // the column does not exist or the user cannot read it.
    Task<TaskColumnContext?> GetColumnContextAsync(
        Guid boardColumnId,
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

    Task<TaskMoveResult> MoveTaskAsync(
        Guid taskId,
        string userId,
        Guid targetColumnId,
        int targetIndex);

    // Task deletion and archiving
    Task<bool> ArchiveTaskAsync(
        Guid taskId,
        string userId);

    Task<bool> DeleteTaskAsync(
        Guid taskId,
        string userId);
}
