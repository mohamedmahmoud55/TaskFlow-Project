using TaskFlow.Services.Boards.Models;

namespace TaskFlow.Services.Boards;

public interface IBoardService
{
    // Board operations
    Task<IReadOnlyList<BoardListItem>> GetBoardsAsync(
        Guid projectId,
        Guid? expectedTeamId,
        string userId);

    Task<BoardDetails?> GetBoardAsync(
        Guid boardId,
        Guid? expectedProjectId,
        string userId);

    Task<BoardCreationResult> CreateBoardAsync(
        Guid projectId,
        Guid? expectedTeamId,
        string userId,
        string name,
        string? description,
        bool isDefault);

    // Column operations
    Task<BoardColumnCreationResult> CreateColumnAsync(
        Guid boardId,
        string userId,
        string name,
        string? description,
        int? wipLimit,
        bool isCompleted);

    Task<bool> UpdateColumnAsync(
        Guid columnId,
        string userId,
        string name,
        string? description,
        int? wipLimit,
        bool isCompleted);

    Task<bool> ReorderColumnsAsync(
        Guid boardId,
        string userId,
        IReadOnlyList<Guid> orderedColumnIds);
    Task<bool> CanManageProjectBoardsAsync(
    Guid projectId,
    string userId);
}