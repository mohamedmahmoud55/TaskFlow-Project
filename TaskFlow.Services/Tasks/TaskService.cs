
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Tasks.Models;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Services.Tasks;

public sealed class TaskService : ITaskService
{
    private readonly ApplicationDbContext _dbContext;

    public TaskService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ---------------------------------------------------------
    // Authorization helpers
    // ---------------------------------------------------------

    private IQueryable<Project> GetAccessibleProjects(string userId)
    {
        return _dbContext.Projects
            .Where(project =>
                !project.IsArchived &&
                !project.Team.IsArchived &&
                !project.Team.Workspace.IsArchived &&
                (
                    project.Team.Workspace.Members.Any(member =>
                        member.UserId == userId &&
                        member.IsActive &&
                        (member.Role == WorkspaceRole.Owner ||
                         member.Role == WorkspaceRole.Admin))
                    ||
                    project.Members.Any(member =>
                        member.UserId == userId &&
                        member.IsActive)
                ));
    }

    private IQueryable<Project> GetManageableProjects(string userId)
    {
        return _dbContext.Projects
            .Where(project =>
                !project.IsArchived &&
                !project.Team.IsArchived &&
                !project.Team.Workspace.IsArchived &&
                (
                    project.Team.Workspace.Members.Any(member =>
                        member.UserId == userId &&
                        member.IsActive &&
                        (member.Role == WorkspaceRole.Owner ||
                         member.Role == WorkspaceRole.Admin))
                    ||
                    project.Members.Any(member =>
                        member.UserId == userId &&
                        member.IsActive &&
                        member.Role == ProjectRole.Manager)
                ));
    }

    private async Task<bool> CanAccessColumnAsync(
        Guid columnId,
        string userId)
    {
        return await _dbContext.BoardColumns
            .AnyAsync(column =>
                column.Id == columnId &&
                !column.Board.IsArchived &&
                GetAccessibleProjects(userId)
                    .Any(project => project.Id == column.Board.ProjectId));
    }

    private async Task<bool> CanManageTaskAsync(
        TaskEntity task,
        string userId)
    {
        var projectId = task.BoardColumn.Board.ProjectId;

        var canAccess = await GetAccessibleProjects(userId)
            .AnyAsync(project => project.Id == projectId);

        if (!canAccess)
            return false;

        var canManageProject = await GetManageableProjects(userId)
            .AnyAsync(project => project.Id == projectId);

        return canManageProject || task.CreatedByUserId == userId;
    }

    // ---------------------------------------------------------
    // Get tasks
    // ---------------------------------------------------------

    public async Task<IReadOnlyList<TaskListItem>> GetTasksAsync(
        Guid boardColumnId,
        string userId)
    {
        if (boardColumnId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<TaskListItem>();
        }

        if (!await CanAccessColumnAsync(boardColumnId, userId))
        {
            return Array.Empty<TaskListItem>();
        }

        return await _dbContext.Tasks
            .AsNoTracking()
            .Where(task =>
                task.BoardColumnId == boardColumnId &&
                !task.IsArchived)
            .OrderBy(task => task.Position)
            .Select(task => new TaskListItem
            {
                Id = task.Id,
                Title = task.Title,
                Priority = task.Priority,
                DueDate = task.DueDate,
                Position = task.Position,
                IsCompleted = task.BoardColumn.IsCompleted,
                IsArchived = task.IsArchived
            })
            .ToListAsync();
    }

    // ---------------------------------------------------------
    // Get task details
    // ---------------------------------------------------------

    public async Task<TaskDetails?> GetTaskAsync(
        Guid taskId,
        Guid? expectedBoardColumnId,
        string userId)
    {
        if (taskId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var query = _dbContext.Tasks
            .AsNoTracking()
            .Where(task =>
                task.Id == taskId &&
                !task.IsArchived &&
                !task.BoardColumn.Board.IsArchived &&
                GetAccessibleProjects(userId)
                    .Any(project =>
                        project.Id == task.BoardColumn.Board.ProjectId));

        if (expectedBoardColumnId.HasValue)
        {
            query = query.Where(task =>
                task.BoardColumnId == expectedBoardColumnId.Value);
        }

        return await query
            .Select(task => new TaskDetails
               {
                Id = task.Id,
              
                BoardColumnId = task.BoardColumnId,
                BoardId = task.BoardColumn.BoardId,

                CreatedByUserId = task.CreatedByUserId,
                Title = task.Title,
                Description = task.Description,
                Priority = task.Priority,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                EstimatedMinutes = task.EstimatedMinutes,
                Position = task.Position,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                CompletedAt = task.CompletedAt,
                IsArchived = task.IsArchived,
                ColumnName = task.BoardColumn.Name,
                IsCompleted = task.BoardColumn.IsCompleted
            })
            .SingleOrDefaultAsync();
    }

    // ---------------------------------------------------------
    // Create task
    // ---------------------------------------------------------

    public async Task<TaskCreationResult> CreateTaskAsync(
        Guid boardColumnId,
        string userId,
        string title,
        string? description,
        TaskPriority priority,
        DateTime? startDate,
        DateTime? dueDate,
        int? estimatedMinutes)
    {
        if (boardColumnId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return TaskCreationResult.Failure(
                "A valid column and authenticated user are required.");
        }

        var trimmedTitle = title?.Trim() ?? string.Empty;

        if (trimmedTitle.Length == 0 || trimmedTitle.Length > 200)
        {
            return TaskCreationResult.Failure(
                "Task title must be between 1 and 200 characters.");
        }

        var normalizedDescription =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        if (normalizedDescription?.Length > 5000)
        {
            return TaskCreationResult.Failure(
                "Task description cannot exceed 5000 characters.");
        }

        if (startDate.HasValue &&
            dueDate.HasValue &&
            dueDate.Value < startDate.Value)
        {
            return TaskCreationResult.Failure(
                "Due date cannot be earlier than start date.");
        }

        if (estimatedMinutes.HasValue &&
            estimatedMinutes.Value <= 0)
        {
            return TaskCreationResult.Failure(
                "Estimated minutes must be greater than zero.");
        }

        if (!Enum.IsDefined(priority))
        {
            return TaskCreationResult.Failure(
                "Invalid task priority.");
        }

        if (!await CanAccessColumnAsync(boardColumnId, userId))
        {
            return TaskCreationResult.Failure(
                "The column could not be found or you do not have access to it.");
        }

        var nextPosition = await _dbContext.Tasks
            .Where(task => task.BoardColumnId == boardColumnId)
            .Select(task => (int?)task.Position)
            .MaxAsync() ?? -1;

        var now = DateTime.UtcNow;

        var task = new TaskEntity
        {
            BoardColumnId = boardColumnId,
            CreatedByUserId = userId,
            Title = trimmedTitle,
            Description = normalizedDescription,
            Priority = priority,
            StartDate = startDate,
            DueDate = dueDate,
            EstimatedMinutes = estimatedMinutes,
            Position = nextPosition + 1,
            CreatedAt = now,
            IsArchived = false
        };

        _dbContext.Tasks.Add(task);

        try
        {
            await _dbContext.SaveChangesAsync();

            return TaskCreationResult.Success(task.Id);
        }
        catch (DbUpdateException)
        {
            return TaskCreationResult.Failure(
                "The task could not be created. Please try again.");
        }
    }

    // ---------------------------------------------------------
    // Update task
    // ---------------------------------------------------------

    public async Task<bool> UpdateTaskAsync(
        Guid taskId,
        string userId,
        string title,
        string? description,
        TaskPriority priority,
        DateTime? startDate,
        DateTime? dueDate,
        int? estimatedMinutes)
    {
        if (taskId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var trimmedTitle = title?.Trim() ?? string.Empty;

        if (trimmedTitle.Length == 0 ||
            trimmedTitle.Length > 200 ||
            (estimatedMinutes.HasValue && estimatedMinutes.Value <= 0) ||
            !Enum.IsDefined(priority))
        {
            return false;
        }

        if (startDate.HasValue &&
            dueDate.HasValue &&
            dueDate.Value < startDate.Value)
        {
            return false;
        }

        var normalizedDescription =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        if (normalizedDescription?.Length > 5000)
            return false;

        var task = await _dbContext.Tasks
            .Include(task => task.BoardColumn)
                .ThenInclude(column => column.Board)
            .SingleOrDefaultAsync(task =>
                task.Id == taskId &&
                !task.IsArchived &&
                !task.BoardColumn.Board.IsArchived);

        if (task is null ||
            !await CanManageTaskAsync(task, userId))
        {
            return false;
        }

        task.Title = trimmedTitle;
        task.Description = normalizedDescription;
        task.Priority = priority;
        task.StartDate = startDate;
        task.DueDate = dueDate;
        task.EstimatedMinutes = estimatedMinutes;
        task.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    // ---------------------------------------------------------
    // Archive task
    // ---------------------------------------------------------

    public async Task<bool> ArchiveTaskAsync(
        Guid taskId,
        string userId)
    {
        if (taskId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var task = await _dbContext.Tasks
            .Include(task => task.BoardColumn)
                .ThenInclude(column => column.Board)
            .SingleOrDefaultAsync(task =>
                task.Id == taskId &&
                !task.IsArchived &&
                !task.BoardColumn.Board.IsArchived);

        if (task is null ||
            !await CanManageTaskAsync(task, userId))
        {
            return false;
        }

        task.IsArchived = true;
        task.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    // ---------------------------------------------------------
    // Delete task
    // ---------------------------------------------------------

    public async Task<bool> DeleteTaskAsync(
        Guid taskId,
        string userId)
    {
        if (taskId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var task = await _dbContext.Tasks
            .Include(task => task.BoardColumn)
                .ThenInclude(column => column.Board)
            .SingleOrDefaultAsync(task =>
                task.Id == taskId);

        if (task is null ||
            !await CanManageTaskAsync(task, userId))
        {
            return false;
        }

        _dbContext.Tasks.Remove(task);

        try
        {
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
