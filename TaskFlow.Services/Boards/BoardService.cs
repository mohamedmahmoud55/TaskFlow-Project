using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Services.Boards.Models;
using TaskFlow.Services.Projects;

namespace TaskFlow.Services.Boards;

public sealed class BoardService : IBoardService
{
    private readonly ApplicationDbContext _dbContext;

    public BoardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ---------------------------------------------------------
    // Authorization helpers (backed by ProjectAccess)
    // ---------------------------------------------------------

    private IQueryable<Project> GetAccessibleProjects(string userId)
    {
        return _dbContext.Projects.ReadableBy(userId);
    }

    private IQueryable<Project> GetManageableProjects(string userId)
    {
        return _dbContext.Projects.ManageableBy(userId);
    }

    private async Task<bool> CanManageBoardAsync(
        Guid boardId,
        string userId)
    {
        return await _dbContext.Boards
            .AnyAsync(board =>
                board.Id == boardId &&
                !board.IsArchived &&
                GetManageableProjects(userId)
                    .Any(project => project.Id == board.ProjectId));
    }

    private async Task<(Board? Board, BoardOperationResult? Error)> LoadManageableBoardAsync(
        Guid boardId,
        string userId)
    {
        if (boardId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return (null, BoardOperationResult.NotFound());
        }

        var board = await _dbContext.Boards
            .SingleOrDefaultAsync(b =>
                b.Id == boardId &&
                !b.IsArchived);

        if (board is null)
        {
            return (null, BoardOperationResult.NotFound());
        }

        var permissions = await ProjectAccess.GetPermissionsAsync(
            _dbContext,
            board.ProjectId,
            userId);

        if (!permissions.CanRead)
        {
            return (null, BoardOperationResult.NotFound());
        }

        if (!permissions.CanManage)
        {
            return (null, BoardOperationResult.Forbidden());
        }

        return (board, null);
    }

    // ---------------------------------------------------------
    // Get boards
    // ---------------------------------------------------------

    public async Task<IReadOnlyList<BoardListItem>> GetBoardsAsync(
        Guid projectId,
        Guid? expectedTeamId,
        string userId)
    {
        if (projectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<BoardListItem>();
        }

        var accessibleProjects = GetAccessibleProjects(userId)
            .Where(project => project.Id == projectId);

        if (expectedTeamId.HasValue)
        {
            accessibleProjects = accessibleProjects
                .Where(project => project.TeamId == expectedTeamId.Value);
        }

        return await _dbContext.Boards
            .AsNoTracking()
            .Where(board =>
                board.ProjectId == projectId &&
                !board.IsArchived &&
                accessibleProjects.Any(
                    project => project.Id == board.ProjectId))
            .OrderByDescending(board => board.IsDefault)
            .ThenBy(board => board.Name)
            .Select(board => new BoardListItem
            {
                Id = board.Id,
                Name = board.Name,
                Description = board.Description,
                IsDefault = board.IsDefault,
                CreatedAt = board.CreatedAt,
                ColumnCount = board.Columns.Count
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<BoardListItem>> GetArchivedBoardsAsync(
        Guid projectId,
        string userId)
    {
        if (projectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<BoardListItem>();
        }

        return await _dbContext.Boards
            .AsNoTracking()
            .Where(board =>
                board.ProjectId == projectId &&
                board.IsArchived &&
                GetManageableProjects(userId)
                    .Any(project => project.Id == board.ProjectId))
            .OrderBy(board => board.Name)
            .Select(board => new BoardListItem
            {
                Id = board.Id,
                Name = board.Name,
                Description = board.Description,
                IsDefault = board.IsDefault,
                CreatedAt = board.CreatedAt,
                ColumnCount = board.Columns.Count
            })
            .ToListAsync();
    }

    // ---------------------------------------------------------
    // Get board details
    // ---------------------------------------------------------

    public async Task<BoardDetails?> GetBoardAsync(
        Guid boardId,
        Guid? expectedProjectId,
        string userId)
    {
        if (boardId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var query = _dbContext.Boards
            .AsNoTracking()
            .Where(board =>
                board.Id == boardId &&
                !board.IsArchived &&
                GetAccessibleProjects(userId)
                    .Any(project => project.Id == board.ProjectId));

        if (expectedProjectId.HasValue)
        {
            query = query.Where(
                board => board.ProjectId == expectedProjectId.Value);
        }

        var details = await query
            .Select(board => new BoardDetails
            {
                Id = board.Id,
                ProjectId = board.ProjectId,
                Name = board.Name,
                Description = board.Description,
                IsDefault = board.IsDefault,
                CreatedAt = board.CreatedAt,

                Columns = board.Columns
                    .OrderBy(column => column.Position)
                    .Select(column => new BoardColumnItem
                    {
                        Id = column.Id,
                        Name = column.Name,
                        Description = column.Description,
                        Position = column.Position,
                        WipLimit = column.WipLimit,
                        IsDefault = column.IsDefault,
                        IsCompleted = column.IsCompleted
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync();

        if (details is null)
        {
            return null;
        }

        var permissions = await ProjectAccess.GetPermissionsAsync(
            _dbContext,
            details.ProjectId,
            userId);

        details.CanManage = permissions.CanManage;
        details.CanCreateTasks = permissions.CanWrite;

        return details;
    }

    // ---------------------------------------------------------
    // Create, update, archive and restore boards
    // ---------------------------------------------------------

    public async Task<BoardCreationResult> CreateBoardAsync(
        Guid projectId,
        Guid? expectedTeamId,
        string userId,
        string name,
        string? description,
        bool isDefault)
    {
        if (projectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return BoardCreationResult.Failure(
                "A valid project and authenticated user are required.");
        }

        var validationError = ValidateBoardInput(
            name,
            description,
            out var trimmedName,
            out var normalizedDescription);

        if (validationError is not null)
        {
            return BoardCreationResult.Failure(validationError);
        }

        var projectQuery = GetManageableProjects(userId)
            .Where(project => project.Id == projectId);

        if (expectedTeamId.HasValue)
        {
            projectQuery = projectQuery
                .Where(project => project.TeamId == expectedTeamId.Value);
        }

        var projectExists = await projectQuery.AnyAsync();

        if (!projectExists)
        {
            return BoardCreationResult.Failure(
                "The project could not be found or you do not have permission to manage it.");
        }

        var hasDefaultBoard = await _dbContext.Boards
            .AnyAsync(board =>
                board.ProjectId == projectId &&
                board.IsDefault);

        var shouldBeDefault = isDefault || !hasDefaultBoard;

        if (isDefault && hasDefaultBoard)
        {
            return BoardCreationResult.Failure(
                "This project already has a default board.");
        }

        var board = new Board
        {
            ProjectId = projectId,
            Name = trimmedName,
            Description = normalizedDescription,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false,
            IsDefault = shouldBeDefault
        };

        // Every new board starts with three default columns.
        board.Columns.Add(new BoardColumn
        {
            BoardId = board.Id,
            Name = "To Do",
            Position = 0,
            IsDefault = true,
            IsCompleted = false
        });

        board.Columns.Add(new BoardColumn
        {
            BoardId = board.Id,
            Name = "In Progress",
            Position = 1,
            IsDefault = false,
            IsCompleted = false
        });

        board.Columns.Add(new BoardColumn
        {
            BoardId = board.Id,
            Name = "Done",
            Position = 2,
            IsDefault = false,
            IsCompleted = true
        });

        _dbContext.Boards.Add(board);

        try
        {
            await _dbContext.SaveChangesAsync();

            return BoardCreationResult.Success(board.Id);
        }
        catch (DbUpdateException)
        {
            return BoardCreationResult.Failure(
                "The board could not be created. Check whether its name or default status is already in use.");
        }
    }

    public async Task<BoardOperationResult> UpdateBoardAsync(
        Guid boardId,
        string userId,
        string name,
        string? description,
        bool isDefault)
    {
        var (board, accessError) = await LoadManageableBoardAsync(boardId, userId);

        if (board is null)
        {
            return accessError ?? BoardOperationResult.NotFound();
        }

        var validationError = ValidateBoardInput(
            name,
            description,
            out var trimmedName,
            out var normalizedDescription);

        if (validationError is not null)
        {
            return BoardOperationResult.Invalid(validationError);
        }

        if (board.IsDefault && !isDefault)
        {
            return BoardOperationResult.Invalid(
                "A project must keep a default board. Mark another board as default instead.");
        }

        var duplicateNameExists = await _dbContext.Boards
            .AnyAsync(candidate =>
                candidate.ProjectId == board.ProjectId &&
                candidate.Id != board.Id &&
                candidate.Name.ToLower() == trimmedName.ToLower());

        if (duplicateNameExists)
        {
            return BoardOperationResult.Invalid(
                "A board with this name already exists in this project.");
        }

        var now = DateTime.UtcNow;

        if (isDefault && !board.IsDefault)
        {
            // Clear the existing default first so the filtered unique index
            // on (ProjectId) WHERE IsDefault = 1 is never violated.
            var currentDefault = await _dbContext.Boards
                .SingleOrDefaultAsync(candidate =>
                    candidate.ProjectId == board.ProjectId &&
                    candidate.IsDefault &&
                    candidate.Id != board.Id);

            return await ExecuteInTransactionAsync(
                async () =>
                {
                    if (currentDefault is not null)
                    {
                        currentDefault.IsDefault = false;
                        currentDefault.UpdatedAt = now;
                        await _dbContext.SaveChangesAsync();
                    }

                    board.Name = trimmedName;
                    board.Description = normalizedDescription;
                    board.IsDefault = true;
                    board.UpdatedAt = now;

                    await _dbContext.SaveChangesAsync();
                    return BoardOperationResult.Success();
                },
                "The board could not be updated.");
        }

        board.Name = trimmedName;
        board.Description = normalizedDescription;
        board.UpdatedAt = now;

        try
        {
            await _dbContext.SaveChangesAsync();
            return BoardOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return BoardOperationResult.Failed("The board could not be updated.");
        }
    }

    public async Task<BoardOperationResult> ArchiveBoardAsync(
        Guid boardId,
        string userId,
        Guid? newDefaultBoardId = null)
    {
        var (board, accessError) = await LoadManageableBoardAsync(boardId, userId);

        if (board is null)
        {
            return accessError ?? BoardOperationResult.NotFound();
        }

        var activeBoards = await _dbContext.Boards
            .Where(candidate =>
                candidate.ProjectId == board.ProjectId &&
                !candidate.IsArchived)
            .ToListAsync();

        if (activeBoards.Count <= 1)
        {
            return BoardOperationResult.Invalid(
                "A project must keep at least one active board.");
        }

        var now = DateTime.UtcNow;

        if (board.IsDefault)
        {
            var replacement = newDefaultBoardId.HasValue
                ? activeBoards.SingleOrDefault(candidate =>
                    candidate.Id == newDefaultBoardId.Value &&
                    candidate.Id != board.Id)
                : null;

            if (replacement is null)
            {
                return BoardOperationResult.Invalid(
                    "Archiving the default board requires choosing a new default board.");
            }

            return await ExecuteInTransactionAsync(
                async () =>
                {
                    board.IsDefault = false;
                    board.IsArchived = true;
                    board.UpdatedAt = now;
                    await _dbContext.SaveChangesAsync();

                    replacement.IsDefault = true;
                    replacement.UpdatedAt = now;
                    await _dbContext.SaveChangesAsync();

                    return BoardOperationResult.Success();
                },
                "The board could not be archived.");
        }

        board.IsArchived = true;
        board.UpdatedAt = now;

        try
        {
            await _dbContext.SaveChangesAsync();
            return BoardOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return BoardOperationResult.Failed("The board could not be archived.");
        }
    }

    public async Task<BoardOperationResult> RestoreBoardAsync(
        Guid boardId,
        string userId)
    {
        if (boardId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return BoardOperationResult.NotFound("Archived board not found.");
        }

        var board = await _dbContext.Boards
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == boardId &&
                candidate.IsArchived &&
                GetManageableProjects(userId)
                    .Any(project => project.Id == candidate.ProjectId));

        if (board is null)
        {
            return BoardOperationResult.NotFound("Archived board not found.");
        }

        var hasDefaultBoard = await _dbContext.Boards
            .AnyAsync(candidate =>
                candidate.ProjectId == board.ProjectId &&
                candidate.IsDefault);

        board.IsArchived = false;
        board.IsDefault = !hasDefaultBoard;
        board.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync();
            return BoardOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return BoardOperationResult.Failed("The board could not be restored.");
        }
    }

    // ---------------------------------------------------------
    // Create, update, reorder and delete columns
    // ---------------------------------------------------------

    public async Task<BoardColumnCreationResult> CreateColumnAsync(
        Guid boardId,
        string userId,
        string name,
        string? description,
        int? wipLimit,
        bool isCompleted)
    {
        if (boardId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return BoardColumnCreationResult.Failure(
                "A valid board and authenticated user are required.");
        }

        var trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0 || trimmedName.Length > 100)
        {
            return BoardColumnCreationResult.Failure(
                "Column name must be between 1 and 100 characters.");
        }

        if (wipLimit.HasValue && wipLimit.Value <= 0)
        {
            return BoardColumnCreationResult.Failure(
                "WIP limit must be greater than zero.");
        }

        var normalizedDescription =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        if (normalizedDescription?.Length > 500)
        {
            return BoardColumnCreationResult.Failure(
                "Column description cannot exceed 500 characters.");
        }

        if (!await CanManageBoardAsync(boardId, userId))
        {
            return BoardColumnCreationResult.Failure(
                "You do not have permission to manage this board.");
        }

        var board = await _dbContext.Boards
            .SingleOrDefaultAsync(board =>
                board.Id == boardId &&
                !board.IsArchived);

        if (board is null)
        {
            return BoardColumnCreationResult.Failure(
                "The board could not be found.");
        }

        var nextPosition = await _dbContext.BoardColumns
            .Where(column => column.BoardId == boardId)
            .Select(column => (int?)column.Position)
            .MaxAsync() ?? -1;

        var column = new BoardColumn
        {
            BoardId = boardId,
            Name = trimmedName,
            Description = normalizedDescription,
            Position = nextPosition + 1,
            WipLimit = wipLimit,
            IsDefault = false,
            IsCompleted = isCompleted,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.BoardColumns.Add(column);

        try
        {
            await _dbContext.SaveChangesAsync();

            return BoardColumnCreationResult.Success(column.Id);
        }
        catch (DbUpdateException)
        {
            return BoardColumnCreationResult.Failure(
                "The column could not be created. Its name may already exist on this board.");
        }
    }

    public async Task<bool> UpdateColumnAsync(
        Guid columnId,
        string userId,
        string name,
        string? description,
        int? wipLimit,
        bool isCompleted)
    {
        if (columnId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0 ||
            trimmedName.Length > 100 ||
            (wipLimit.HasValue && wipLimit.Value <= 0))
        {
            return false;
        }

        var normalizedDescription =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        if (normalizedDescription?.Length > 500)
        {
            return false;
        }

        var column = await _dbContext.BoardColumns
            .Include(column => column.Board)
            .SingleOrDefaultAsync(column =>
                column.Id == columnId &&
                !column.Board.IsArchived);

        if (column is null ||
            !await CanManageBoardAsync(column.BoardId, userId))
        {
            return false;
        }

        column.Name = trimmedName;
        column.Description = normalizedDescription;
        column.WipLimit = wipLimit;
        column.IsCompleted = isCompleted;

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

    public async Task<bool> ReorderColumnsAsync(
        Guid boardId,
        string userId,
        IReadOnlyList<Guid> orderedColumnIds)
    {
        if (boardId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId) ||
            orderedColumnIds is null ||
            orderedColumnIds.Count == 0 ||
            orderedColumnIds.Any(id => id == Guid.Empty) ||
            orderedColumnIds.Distinct().Count() != orderedColumnIds.Count)
        {
            return false;
        }

        if (!await CanManageBoardAsync(boardId, userId))
        {
            return false;
        }

        var columns = await _dbContext.BoardColumns
            .Where(column => column.BoardId == boardId)
            .ToListAsync();

        // Every existing column must appear exactly once.
        if (columns.Count != orderedColumnIds.Count ||
            !columns.Select(column => column.Id)
                .ToHashSet()
                .SetEquals(orderedColumnIds))
        {
            return false;
        }

        var result = await ExecuteInTransactionAsync(
            async () =>
            {
                // First move every column to a unique temporary position.
                for (var index = 0; index < orderedColumnIds.Count; index++)
                {
                    var column = columns.Single(
                        item => item.Id == orderedColumnIds[index]);

                    column.Position = -(index + 1);
                }

                await _dbContext.SaveChangesAsync();

                // Then assign the final sequential positions.
                for (var index = 0; index < orderedColumnIds.Count; index++)
                {
                    var column = columns.Single(
                        item => item.Id == orderedColumnIds[index]);

                    column.Position = index;
                }

                await _dbContext.SaveChangesAsync();
                return BoardOperationResult.Success();
            },
            "The column order could not be updated.");

        return result.Succeeded;
    }

    public async Task<BoardOperationResult> DeleteColumnAsync(
        Guid columnId,
        string userId)
    {
        if (columnId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return BoardOperationResult.NotFound("The column could not be found.");
        }

        var column = await _dbContext.BoardColumns
            .Include(c => c.Board)
            .SingleOrDefaultAsync(c =>
                c.Id == columnId &&
                !c.Board.IsArchived);

        if (column is null)
        {
            return BoardOperationResult.NotFound("The column could not be found.");
        }

        var permissions = await ProjectAccess.GetPermissionsAsync(
            _dbContext,
            column.Board.ProjectId,
            userId);

        if (!permissions.CanRead)
        {
            return BoardOperationResult.NotFound("The column could not be found.");
        }

        if (!permissions.CanManage)
        {
            return BoardOperationResult.Forbidden();
        }

        if (column.IsDefault)
        {
            return BoardOperationResult.Invalid("The default column cannot be deleted.");
        }

        var columns = await _dbContext.BoardColumns
            .Where(c => c.BoardId == column.BoardId)
            .OrderBy(c => c.Position)
            .ToListAsync();

        if (columns.Count <= 1)
        {
            return BoardOperationResult.Invalid("A board must keep at least one column.");
        }

        var hasActiveTasks = await _dbContext.Tasks
            .AnyAsync(t => t.BoardColumnId == columnId && !t.IsArchived);

        if (hasActiveTasks)
        {
            return BoardOperationResult.Invalid(
                "This column still has tasks. Move the tasks to another column before deleting it.");
        }

        // Archived tasks have a Restrict FK to BoardColumn, so move any archived
        // tasks to another column (preferring the default column) before deletion.
        var fallbackColumn = columns.FirstOrDefault(c => c.IsDefault && c.Id != columnId)
            ?? columns.First(c => c.Id != columnId);

        var archivedTasks = await _dbContext.Tasks
            .Where(t => t.BoardColumnId == columnId && t.IsArchived)
            .OrderBy(t => t.Position)
            .ToListAsync();

        var remainingColumns = columns
            .Where(c => c.Id != columnId)
            .OrderBy(c => c.Position)
            .ToList();

        return await ExecuteInTransactionAsync(
            async () =>
            {
                if (archivedTasks.Count > 0)
                {
                    var nextFallbackPosition = await _dbContext.Tasks
                        .Where(t => t.BoardColumnId == fallbackColumn.Id)
                        .Select(t => (int?)t.Position)
                        .MaxAsync() ?? -1;

                    foreach (var archivedTask in archivedTasks)
                    {
                        nextFallbackPosition++;
                        archivedTask.BoardColumnId = fallbackColumn.Id;
                        archivedTask.Position = nextFallbackPosition;
                    }

                    await _dbContext.SaveChangesAsync();
                }

                _dbContext.BoardColumns.Remove(column);
                await _dbContext.SaveChangesAsync();

                // Normalize remaining column positions to 0 .. n-1.
                for (var index = 0; index < remainingColumns.Count; index++)
                {
                    remainingColumns[index].Position = -(index + 1);
                }

                await _dbContext.SaveChangesAsync();

                for (var index = 0; index < remainingColumns.Count; index++)
                {
                    remainingColumns[index].Position = index;
                }

                await _dbContext.SaveChangesAsync();

                return BoardOperationResult.Success();
            },
            "The column could not be deleted.");
    }

    public async Task<bool> CanManageProjectBoardsAsync(
        Guid projectId,
        string userId)
    {
        if (projectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await GetManageableProjects(userId)
            .AnyAsync(project => project.Id == projectId);
    }

    // ---------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------

    private async Task<BoardOperationResult> ExecuteInTransactionAsync(
        Func<Task<BoardOperationResult>> operation,
        string failureMessage)
    {
        var existingTransaction = _dbContext.Database.CurrentTransaction;
        var ownsTransaction = existingTransaction is null;
        IDbContextTransaction transaction = ownsTransaction
            ? await _dbContext.Database.BeginTransactionAsync()
            : existingTransaction!;

        const string savepointName = "BoardOp";
        if (!ownsTransaction)
        {
            await transaction.CreateSavepointAsync(savepointName);
        }

        try
        {
            var result = await operation();

            if (ownsTransaction)
            {
                await transaction.CommitAsync();
            }

            return result;
        }
        catch (DbUpdateException)
        {
            if (ownsTransaction)
            {
                await transaction.RollbackAsync();
            }
            else
            {
                await transaction.RollbackToSavepointAsync(savepointName);
            }

            return BoardOperationResult.Failed(failureMessage);
        }
        finally
        {
            if (ownsTransaction)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static string? ValidateBoardInput(
        string? name,
        string? description,
        out string trimmedName,
        out string? normalizedDescription)
    {
        trimmedName = name?.Trim() ?? string.Empty;

        normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (trimmedName.Length == 0 || trimmedName.Length > 150)
        {
            return "Board name must be between 1 and 150 characters.";
        }

        if (normalizedDescription?.Length > 1000)
        {
            return "Board description cannot exceed 1000 characters.";
        }

        return null;
    }
}
