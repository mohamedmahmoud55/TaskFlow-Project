
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Boards.Models;

namespace TaskFlow.Services.Boards;

public sealed class BoardService : IBoardService
{
    private readonly ApplicationDbContext _dbContext;

    public BoardService(ApplicationDbContext dbContext)
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

        return await query
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
    }

    // ---------------------------------------------------------
    // Create board
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

        var trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0 || trimmedName.Length > 150)
        {
            return BoardCreationResult.Failure(
                "Board name must be between 1 and 150 characters.");
        }

        var normalizedDescription =
            string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

        if (normalizedDescription?.Length > 1000)
        {
            return BoardCreationResult.Failure(
                "Board description cannot exceed 1000 characters.");
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

    // ---------------------------------------------------------
    // Create column
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

    // ---------------------------------------------------------
    // Update column
    // ---------------------------------------------------------

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

    // ---------------------------------------------------------
    // Reorder columns
    // ---------------------------------------------------------

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

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync();

        try
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
            await transaction.CommitAsync();

            return true;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return false;
        }

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
}
