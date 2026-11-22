using TaskFlow.Data.Enums;
using TaskFlow.Services.Workspaces.Models;

namespace TaskFlow.Web.Models;

public sealed class WorkspaceDetailsViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public WorkspaceRole CurrentUserRole { get; init; }

    public bool CanManageMembers { get; init; }

    // The roles the current user may give to other people.
    public IReadOnlyList<WorkspaceRole> AssignableRoles { get; init; }
        = Array.Empty<WorkspaceRole>();

    public IReadOnlyList<WorkspaceMemberItem> Members { get; init; }
        = Array.Empty<WorkspaceMemberItem>();

    // Owners and Admins can edit the workspace; only the Owner can archive it.
    public bool CanEditWorkspace { get; init; }

    public bool CanArchiveWorkspace { get; init; }
}
