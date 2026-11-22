using TaskFlow.Data.Enums;
using TaskFlow.Services.Teams.Models;

namespace TaskFlow.Web.Models;

public sealed class TeamDetailsViewModel
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; init; }

    public string WorkspaceName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public TeamRole? CurrentUserRole { get; init; }

    public bool CanManageMembers { get; init; }

    // The roles the current user may give to other people on this team.
    public IReadOnlyList<TeamRole> AssignableRoles { get; init; }
        = Array.Empty<TeamRole>();

    // Workspace members who can be added to the team.
    public IReadOnlyList<TeamMemberCandidate> Candidates { get; init; }
        = Array.Empty<TeamMemberCandidate>();

    public IReadOnlyList<TeamMemberItem> Members { get; init; }
        = Array.Empty<TeamMemberItem>();

    // Whether the user may edit or archive the team itself.
    public bool CanEditTeam { get; init; }

    public bool CanArchiveTeam { get; init; }
}
