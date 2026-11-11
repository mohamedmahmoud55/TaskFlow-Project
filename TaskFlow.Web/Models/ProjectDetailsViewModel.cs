
using TaskFlow.Data.Enums;
using TaskFlow.Services.Projects.Models;

namespace TaskFlow.Web.Models;

public sealed class ProjectDetailsViewModel
{
    public Guid Id { get; init; }

    public Guid TeamId { get; init; }

    public Guid WorkspaceId { get; init; }

    public string WorkspaceName { get; init; } = string.Empty;

    public string TeamName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public ProjectStatus Status { get; init; }

    public ProjectRole? CurrentUserRole { get; init; }

    public bool CanManageMembers { get; init; }

    public IReadOnlyList<ProjectMemberItem> Members { get; init; }
        = Array.Empty<ProjectMemberItem>();
}
