using TaskFlow.Services.Workspaces.Models;

namespace TaskFlow.Web.Models;

public sealed class WorkspaceListViewModel
{
    public IReadOnlyList<WorkspaceListItem> Workspaces { get; init; }
        = Array.Empty<WorkspaceListItem>();
}
