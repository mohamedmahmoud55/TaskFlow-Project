using TaskFlow.Services.Workspaces.Models;

namespace TaskFlow.Services.Workspaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceListItem>> GetUserWorkspacesAsync(string userId);

    Task<WorkspaceDetails?> GetWorkspaceAsync(Guid workspaceId, string userId);

    Task<WorkspaceCreationResult> CreateWorkspaceAsync(
        string userId,
        string name,
        string? description);
}
