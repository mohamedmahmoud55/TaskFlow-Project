using TaskFlow.Services.Workspaces.Models;

namespace TaskFlow.Web.Models;

public sealed class WorkspaceListViewModel
{
    // True when the page lists archived workspaces the user can restore.
    public bool ShowingArchived { get; init; }

    public IReadOnlyList<WorkspaceListItem> Workspaces { get; init; }
        = Array.Empty<WorkspaceListItem>();
}
