using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class TeamDetailsViewModel
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; init; }

    public string WorkspaceName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public TeamRole? CurrentUserRole { get; init; }
}
