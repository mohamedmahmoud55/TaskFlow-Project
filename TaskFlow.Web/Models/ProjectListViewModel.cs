using TaskFlow.Services.Projects.Models;

namespace TaskFlow.Web.Models;

public sealed class ProjectListViewModel
{
    public Guid TeamId { get; init; }

    public Guid WorkspaceId { get; init; }

    public string WorkspaceName { get; init; } = string.Empty;

    public string TeamName { get; init; } = string.Empty;

    public IReadOnlyList<ProjectListItem> Projects { get; init; }
        = Array.Empty<ProjectListItem>();
}
