using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class WorkspaceDetailsViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public WorkspaceRole CurrentUserRole { get; init; }
}
