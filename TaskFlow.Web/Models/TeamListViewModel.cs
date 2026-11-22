using TaskFlow.Services.Teams.Models;

namespace TaskFlow.Web.Models;

public sealed class TeamListViewModel
{
    public Guid WorkspaceId { get; init; }

    public string WorkspaceName { get; init; } = string.Empty;

    public IReadOnlyList<TeamListItem> Teams { get; init; }
        = Array.Empty<TeamListItem>();

    // Archived teams the user can restore (workspace Owners and Admins only).
    public IReadOnlyList<TeamListItem> ArchivedTeams { get; init; }
        = Array.Empty<TeamListItem>();
}
