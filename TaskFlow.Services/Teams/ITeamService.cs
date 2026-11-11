using TaskFlow.Services.Teams.Models;

namespace TaskFlow.Services.Teams;

public interface ITeamService
{
    Task<IReadOnlyList<TeamListItem>> GetTeamsAsync(Guid workspaceId, string userId);

    Task<TeamDetails?> GetTeamAsync(Guid teamId, Guid? expectedWorkspaceId, string userId);

    Task<TeamCreationResult> CreateTeamAsync(
        Guid workspaceId,
        string userId,
        string name,
        string? description);
}
