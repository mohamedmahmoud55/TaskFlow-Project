using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Projects;
using TaskFlow.Services.Tasks.Models;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Services.Tasks;

public sealed class TaskService : ITaskService
{
    private readonly ApplicationDbContext _dbContext;

    // Allows integration tests to inject a deterministic unique-index race
    // right before MoveTaskAsync or CreateTaskAsync saves.
    internal Func<int, System.Threading.Tasks.Task>? BeforePositionSaveForTest { get; set; }

    public TaskService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ---------------------------------------------------------
    // Authorization helpers
    //
    // The rules themselves live in ProjectAccess, so every service
    // applies the same definition of read, write and manage.
    // ---------------------------------------------------------

    private IQueryable<Project> GetAccessibleProjects(string userId)
    {
        return _dbContext.Projects.ReadableBy(userId);
    }

    private IQueryable<Project> GetWritableProjects(string userId)
    {
        return _dbContext.Projects.WritableBy(userId);
    }

    private IQueryable<Project> GetManageableProjects(string userId)
    {
        return _dbContext.Projects.ManageableBy(userId);
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

    private async Task<bool> CanWriteColumnAsync(
        Guid columnId,
        string userId)
    {
        return await _dbContext.BoardColumns
            .AnyAsync(column =>
                column.Id == columnId &&
                !column.Board.IsArchived &&
                GetWritableProjects(userId)
                    .Any(project => project.Id == column.Board.ProjectId));
    }

        var canAccess = await GetAccessibleProjects(userId)
            .AnyAsync(project => project.Id == projectId);

        if (!canAccess)
            return false;

    private async Task<bool> CanEditTaskAsync(
        TaskEntity task,
        string userId)
    {
        var permissions = await GetProjectPermissionsAsync(
            task.BoardColumn.Board.ProjectId,
            userId);

        return CanEditTask(permissions, task.CreatedByUserId, userId);
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

        var projectId = await _dbContext.BoardColumns
            .Where(column =>
                column.Id == boardColumnId &&
                !column.Board.IsArchived)
            .Select(column => (Guid?)column.Board.ProjectId)
            .SingleOrDefaultAsync();

        if (projectId is null)
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

    private static TaskAssigneeBadge BuildAssigneeBadge(
        string userId,
        string? firstName,
        string? lastName,
        string? userName,
        string? email)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        var displayName = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : userName ?? email ?? userId;

        string initials;
        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
        {
            initials = $"{char.ToUpperInvariant(firstName.Trim()[0])}{char.ToUpperInvariant(lastName.Trim()[0])}";
        }
        else if (!string.IsNullOrWhiteSpace(firstName))
        {
            initials = char.ToUpperInvariant(firstName.Trim()[0]).ToString();
        }
        else
        {
            var source = (email ?? userName ?? userId).Trim();
            initials = source.Length >= 2
                ? source[..2].ToUpperInvariant()
                : source.ToUpperInvariant();
        }

        return new TaskAssigneeBadge(userId, displayName, initials);
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

        var details = await query
            .Select(task => new TaskDetails
            {
                Id = task.Id,

                BoardColumnId = task.BoardColumnId,
                BoardId = task.BoardColumn.BoardId,
                ProjectId = task.BoardColumn.Board.ProjectId,

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
    // Can the user add tasks to a column?
    // ---------------------------------------------------------

    public async Task<bool> CanCreateTasksAsync(
        Guid boardColumnId,
        string userId)
    {
        if (boardColumnId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await CanWriteColumnAsync(boardColumnId, userId);
    }

    // ---------------------------------------------------------
    // Where does a column sit?
    // ---------------------------------------------------------

    public async Task<TaskColumnContext?> GetColumnContextAsync(
        Guid boardColumnId,
        string userId)
    {
        if (boardColumnId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var context = await _dbContext.BoardColumns
            .AsNoTracking()
            .Where(column =>
                column.Id == boardColumnId &&
                !column.Board.IsArchived)
            .Select(column => new TaskColumnContext
            {
                ProjectId = column.Board.ProjectId,
                BoardId = column.BoardId,
                BoardName = column.Board.Name,
                ColumnName = column.Name
            })
            .SingleOrDefaultAsync();

        if (context is null)
        {
            return null;
        }

        var permissions = await GetProjectPermissionsAsync(
            context.ProjectId,
            userId);

        return permissions.CanRead ? context : null;
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

        var validationError = ValidateTaskInput(
            title,
            description,
            priority,
            startDate,
            dueDate,
            estimatedMinutes,
            out var trimmedTitle,
            out var normalizedDescription);

        if (validationError is not null)
        {
            return TaskCreationResult.Failure(validationError);
        }

        if (!await CanAccessColumnAsync(boardColumnId, userId))
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
                CompletedAt = column.IsCompleted ? now : null,
                IsArchived = false
            };

            _dbContext.Tasks.Add(task);

            try
            {
                if (BeforePositionSaveForTest is not null)
                {
                    await BeforePositionSaveForTest(attempt);
                }

                await _dbContext.SaveChangesAsync();

                return TaskCreationResult.Success(task.Id);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt == 0)
            {
                _dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return TaskCreationResult.Failure(
                    "The board changed, please try again.");
            }
            catch (DbUpdateException)
            {
                return TaskCreationResult.Failure(
                    "The task could not be created. Please try again.");
            }
        }

        return TaskCreationResult.Failure(
            "The board changed, please try again.");
    }

    // ---------------------------------------------------------
    // Update task
    // ---------------------------------------------------------

    public async Task<TaskUpdateResult> UpdateTaskAsync(
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
            return TaskUpdateResult.NotFound();
        }

        var task = await _dbContext.Tasks
            .Include(task => task.BoardColumn)
                .ThenInclude(column => column.Board)
            .SingleOrDefaultAsync(task =>
                task.Id == taskId &&
                !task.IsArchived &&
                !task.BoardColumn.Board.IsArchived);

        if (task is null)
        {
            return TaskUpdateResult.NotFound();
        }

        // A task the user cannot read is reported as not found, so its
        // existence is not revealed to people outside the project.
        var projectId = task.BoardColumn.Board.ProjectId;

        if (!await GetAccessibleProjects(userId)
                .AnyAsync(project => project.Id == projectId))
        {
            return TaskUpdateResult.NotFound();
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
            return TaskUpdateResult.Invalid(validationError);
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
            !await CanEditTaskAsync(task, userId))
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
    // Delete task (Managers and workspace admins only)
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

    // ---------------------------------------------------------
    // Validation
    // ---------------------------------------------------------

    private static string? ValidateTaskInput(
        string? title,
        string? description,
        TaskPriority priority,
        DateTime? startDate,
        DateTime? dueDate,
        int? estimatedMinutes,
        out string trimmedTitle,
        out string? normalizedDescription)
    {
        trimmedTitle = title?.Trim() ?? string.Empty;

        normalizedDescription =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        if (trimmedTitle.Length == 0 || trimmedTitle.Length > 200)
            return "Task title must be between 1 and 200 characters.";

        if (normalizedDescription?.Length > 5000)
            return "Task description cannot exceed 5000 characters.";

        if (startDate.HasValue &&
            dueDate.HasValue &&
            dueDate.Value < startDate.Value)
        {
            return "Due date cannot be earlier than start date.";
        }

        if (estimatedMinutes.HasValue &&
            estimatedMinutes.Value <= 0)
        {
            return "Estimated minutes must be greater than zero.";
        }

        if (!Enum.IsDefined(priority))
            return "Invalid task priority.";

        return null;
    }
}
