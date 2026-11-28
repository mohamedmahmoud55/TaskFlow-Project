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

    private Task<ProjectPermissions> GetProjectPermissionsAsync(
        Guid projectId,
        string userId)
    {
        return ProjectAccess.GetPermissionsAsync(_dbContext, projectId, userId);
    }

    // A task can be edited or archived by anyone who manages the project,
    // or by its creator while that person can still contribute.
    // A Viewer therefore cannot change even a task they created earlier.
    // This is the only place that rule is written down: the checks that
    // enforce it and the CanEdit flags shown in the UI both use it.
    private static bool CanEditTask(
        ProjectPermissions permissions,
        string createdByUserId,
        string userId)
    {
        return permissions.CanManage ||
            (permissions.CanWrite && createdByUserId == userId);
    }

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

        var access = await _dbContext.BoardColumns
            .AsNoTracking()
            .Where(column =>
                column.Id == boardColumnId &&
                !column.Board.IsArchived)
            .Select(column => new
            {
                CanRead = _dbContext.Projects.ReadableBy(userId)
                    .Any(p => p.Id == column.Board.ProjectId),
                CanWrite = _dbContext.Projects.WritableBy(userId)
                    .Any(p => p.Id == column.Board.ProjectId),
                CanManage = _dbContext.Projects.ManageableBy(userId)
                    .Any(p => p.Id == column.Board.ProjectId)
            })
            .SingleOrDefaultAsync();

        if (access is null || !access.CanRead)
        {
            return Array.Empty<TaskListItem>();
        }

        var permissions = new ProjectPermissions(
            access.CanRead,
            access.CanWrite,
            access.CanManage);

        var query = _dbContext.Tasks
            .AsNoTracking()
            .Where(task =>
                task.BoardColumnId == boardColumnId &&
                !task.IsArchived)
            .OrderBy(task => task.Position);

        return await ProjectTaskListItemsAsync(query, permissions, userId);
    }

    public async Task<IReadOnlyList<TaskListItem>> GetBoardTasksAsync(
        Guid boardId,
        string userId)
    {
        if (boardId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<TaskListItem>();
        }

        // Fixed query 1: verify board is unarchived and resolve project permissions once.
        var access = await _dbContext.Boards
            .AsNoTracking()
            .Where(board =>
                board.Id == boardId &&
                !board.IsArchived)
            .Select(board => new
            {
                CanRead = _dbContext.Projects.ReadableBy(userId)
                    .Any(p => p.Id == board.ProjectId),
                CanWrite = _dbContext.Projects.WritableBy(userId)
                    .Any(p => p.Id == board.ProjectId),
                CanManage = _dbContext.Projects.ManageableBy(userId)
                    .Any(p => p.Id == board.ProjectId)
            })
            .SingleOrDefaultAsync();

        if (access is null || !access.CanRead)
        {
            return Array.Empty<TaskListItem>();
        }

        var permissions = new ProjectPermissions(
            access.CanRead,
            access.CanWrite,
            access.CanManage);

        // Fixed query 2: fetch all active tasks across every column in one query.
        var query = _dbContext.Tasks
            .AsNoTracking()
            .Where(task =>
                task.BoardColumn.BoardId == boardId &&
                !task.IsArchived)
            .OrderBy(task => task.BoardColumn.Position)
            .ThenBy(task => task.Position);

        return await ProjectTaskListItemsAsync(query, permissions, userId);
    }

    private static async Task<IReadOnlyList<TaskListItem>> ProjectTaskListItemsAsync(
        IQueryable<TaskEntity> query,
        ProjectPermissions permissions,
        string userId)
    {
        var rows = await query
            .Select(task => new
            {
                task.Id,
                task.BoardColumnId,
                task.Title,
                task.Priority,
                task.DueDate,
                task.Position,
                IsCompleted = task.BoardColumn.IsCompleted,
                task.CompletedAt,
                task.IsArchived,
                task.CreatedByUserId,
                Assignees = task.Assignees
                    .OrderBy(a => a.User.FirstName)
                    .ThenBy(a => a.User.LastName)
                    .ThenBy(a => a.User.Email)
                    .Select(a => new
                    {
                        a.UserId,
                        a.User.FirstName,
                        a.User.LastName,
                        a.User.UserName,
                        a.User.Email
                    })
                    .ToList()
            })
            .ToListAsync();

        return rows.Select(task => new TaskListItem
        {
            Id = task.Id,
            BoardColumnId = task.BoardColumnId,
            Title = task.Title,
            Priority = task.Priority,
            DueDate = task.DueDate,
            Position = task.Position,
            IsCompleted = task.IsCompleted,
            CompletedAt = task.CompletedAt,
            IsArchived = task.IsArchived,
            CreatedByUserId = task.CreatedByUserId,
            CanEdit = CanEditTask(permissions, task.CreatedByUserId, userId),
            Assignees = task.Assignees
                .Select(a => BuildAssigneeBadge(
                    a.UserId,
                    a.FirstName,
                    a.LastName,
                    a.UserName,
                    a.Email))
                .ToList()
        }).ToList();
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

        if (details is null)
        {
            return null;
        }

        var permissions = await GetProjectPermissionsAsync(
            details.ProjectId,
            userId);

        details.CanEdit = CanEditTask(
            permissions,
            details.CreatedByUserId,
            userId);

        return details;
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
                "The column could not be found or you do not have access to it.");
        }

        if (!await CanWriteColumnAsync(boardColumnId, userId))
        {
            return TaskCreationResult.Failure(
                "You have view-only access to this project, so you cannot add tasks to it.");
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var column = await _dbContext.BoardColumns
                .AsNoTracking()
                .Where(c => c.Id == boardColumnId && !c.Board.IsArchived)
                .Select(c => new
                {
                    c.Name,
                    c.WipLimit,
                    c.IsCompleted
                })
                .SingleOrDefaultAsync();

            if (column is null)
            {
                return TaskCreationResult.Failure(
                    "The column could not be found or you do not have access to it.");
            }

            if (column.WipLimit.HasValue)
            {
                var activeCount = await _dbContext.Tasks
                    .CountAsync(t => t.BoardColumnId == boardColumnId && !t.IsArchived);

                if (activeCount >= column.WipLimit.Value)
                {
                    return TaskCreationResult.Failure(
                        $"The column {column.Name} has reached its WIP limit of {column.WipLimit.Value}.");
                }
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

        if (!await CanEditTaskAsync(task, userId))
        {
            return TaskUpdateResult.Forbidden(
                "You do not have permission to edit this task.");
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
            return TaskUpdateResult.Success();
        }
        catch (DbUpdateException)
        {
            return TaskUpdateResult.Failed(
                "The task could not be saved. Please try again.");
        }
    }

    // ---------------------------------------------------------
    // Move task (between or within columns)
    // ---------------------------------------------------------

    public async Task<TaskMoveResult> MoveTaskAsync(
        Guid taskId,
        string userId,
        Guid targetColumnId,
        int targetIndex)
    {
        if (taskId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return TaskMoveResult.NotFound();
        }

        if (targetColumnId == Guid.Empty)
        {
            return TaskMoveResult.NotFound("The target column could not be found.");
        }

        if (targetIndex < 0)
        {
            return TaskMoveResult.Invalid("Invalid target position.");
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var task = await _dbContext.Tasks
                .Include(t => t.BoardColumn)
                    .ThenInclude(c => c.Board)
                .SingleOrDefaultAsync(t =>
                    t.Id == taskId &&
                    !t.IsArchived &&
                    !t.BoardColumn.Board.IsArchived);

            if (task is null)
            {
                return TaskMoveResult.NotFound();
            }

            var sourceProjectId = task.BoardColumn.Board.ProjectId;
            var permissions = await GetProjectPermissionsAsync(sourceProjectId, userId);

            if (!permissions.CanRead)
            {
                return TaskMoveResult.NotFound();
            }

            if (!permissions.CanWrite)
            {
                return TaskMoveResult.Forbidden(
                    "You have view-only access to this project, so you cannot move tasks.");
            }

            var targetColumn = await _dbContext.BoardColumns
                .Include(c => c.Board)
                .SingleOrDefaultAsync(c =>
                    c.Id == targetColumnId &&
                    !c.Board.IsArchived);

            if (targetColumn is null)
            {
                return TaskMoveResult.NotFound("The target column could not be found.");
            }

            if (targetColumn.Board.ProjectId != sourceProjectId)
            {
                var targetProjectPermissions = await GetProjectPermissionsAsync(
                    targetColumn.Board.ProjectId,
                    userId);

                return targetProjectPermissions.CanRead
                    ? TaskMoveResult.Invalid("Tasks can only be moved between columns on the same board.")
                    : TaskMoveResult.NotFound("The target column could not be found.");
            }

            if (targetColumn.BoardId != task.BoardColumn.BoardId)
            {
                return TaskMoveResult.Invalid(
                    "Tasks can only be moved between columns on the same board.");
            }

            var sourceColumnId = task.BoardColumnId;
            var sameColumn = sourceColumnId == targetColumnId;

            // Load all tasks (active and archived) in the affected column(s)
            // because the unique index on (BoardColumnId, Position) covers
            // archived rows too.
            var affectedTasks = await _dbContext.Tasks
                .Where(t =>
                    t.BoardColumnId == sourceColumnId ||
                    t.BoardColumnId == targetColumnId)
                .OrderBy(t => t.Position)
                .ToListAsync();

            var sourceActive = affectedTasks
                .Where(t => t.BoardColumnId == sourceColumnId && !t.IsArchived)
                .OrderBy(t => t.Position)
                .ToList();

            var targetActive = sameColumn
                ? sourceActive
                : affectedTasks
                    .Where(t => t.BoardColumnId == targetColumnId && !t.IsArchived)
                    .OrderBy(t => t.Position)
                    .ToList();

            if (!sameColumn &&
                targetColumn.WipLimit.HasValue &&
                targetActive.Count >= targetColumn.WipLimit.Value)
            {
                return TaskMoveResult.WipLimit(
                    $"The column {targetColumn.Name} has reached its WIP limit of {targetColumn.WipLimit.Value}.");
            }

            sourceActive.RemoveAll(t => t.Id == task.Id);

            var clampedIndex = Math.Min(targetIndex, targetActive.Count);
            targetActive.Insert(clampedIndex, task);

            var now = DateTime.UtcNow;
            task.BoardColumnId = targetColumnId;
            task.UpdatedAt = now;

            if (targetColumn.IsCompleted)
            {
                task.CompletedAt ??= now;
            }
            else
            {
                task.CompletedAt = null;
            }

            var sourceArchived = affectedTasks
                .Where(t => t.BoardColumnId == sourceColumnId && t.IsArchived && t.Id != task.Id)
                .OrderBy(t => t.Position)
                .ToList();

            var targetArchived = sameColumn
                ? sourceArchived
                : affectedTasks
                    .Where(t => t.BoardColumnId == targetColumnId && t.IsArchived)
                    .OrderBy(t => t.Position)
                    .ToList();

            var existingTransaction = _dbContext.Database.CurrentTransaction;
            var ownsTransaction = existingTransaction is null;
            IDbContextTransaction transaction = ownsTransaction
                ? await _dbContext.Database.BeginTransactionAsync()
                : existingTransaction!;

            const string savepointName = "MoveTask";
            if (!ownsTransaction)
            {
                await transaction.CreateSavepointAsync(savepointName);
            }

            try
            {
                if (BeforePositionSaveForTest is not null)
                {
                    await BeforePositionSaveForTest(attempt);
                }

                // Phase 1: assign unique negative temporary positions across all
                // rows in the affected columns so swaps never collide on the
                // unique index (BoardColumnId, Position).
                var tempPosition = 0;
                foreach (var row in affectedTasks)
                {
                    row.Position = --tempPosition;
                }

                await _dbContext.SaveChangesAsync();

                // Phase 2: write final contiguous 0 .. n-1 positions for active
                // tasks, followed by any archived tasks in each column.
                AssignSequentialPositions(sourceActive, sourceArchived);

                if (!sameColumn)
                {
                    AssignSequentialPositions(targetActive, targetArchived);
                }

                await _dbContext.SaveChangesAsync();

                if (ownsTransaction)
                {
                    await transaction.CommitAsync();
                }

                return TaskMoveResult.Success(targetColumn.IsCompleted);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt == 0)
            {
                await RollbackMoveAsync(transaction, ownsTransaction, savepointName);
                _dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await RollbackMoveAsync(transaction, ownsTransaction, savepointName);
                return TaskMoveResult.Conflict("The board changed, please try again.");
            }
            catch (DbUpdateException)
            {
                await RollbackMoveAsync(transaction, ownsTransaction, savepointName);
                return TaskMoveResult.Conflict("The board changed, please try again.");
            }
            finally
            {
                if (ownsTransaction)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        return TaskMoveResult.Conflict("The board changed, please try again.");
    }

    private static void AssignSequentialPositions(
        IReadOnlyList<TaskEntity> activeTasks,
        IReadOnlyList<TaskEntity> archivedTasks)
    {
        for (var i = 0; i < activeTasks.Count; i++)
        {
            activeTasks[i].Position = i;
        }

        for (var i = 0; i < archivedTasks.Count; i++)
        {
            archivedTasks[i].Position = activeTasks.Count + i;
        }
    }

    private static async System.Threading.Tasks.Task RollbackMoveAsync(
        IDbContextTransaction transaction,
        bool ownsTransaction,
        string savepointName)
    {
        if (ownsTransaction)
        {
            await transaction.RollbackAsync();
        }
        else
        {
            await transaction.RollbackToSavepointAsync(savepointName);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                return true;
            }
        }

        return false;
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

        if (task is null)
        {
            return false;
        }

        var canManage = await GetManageableProjects(userId)
            .AnyAsync(project => project.Id == task.BoardColumn.Board.ProjectId);

        if (!canManage)
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
