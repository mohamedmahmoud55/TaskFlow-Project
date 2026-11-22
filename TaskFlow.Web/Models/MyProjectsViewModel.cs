using TaskFlow.Services.Projects.Models;

namespace TaskFlow.Web.Models;

public sealed class MyProjectsViewModel
{
    // True when the page lists archived projects the user can restore.
    public bool ShowingArchived { get; init; }

    public IReadOnlyList<MyProjectItem> Projects { get; init; }
        = Array.Empty<MyProjectItem>();
}
