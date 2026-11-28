using TaskFlow.Services.Boards.Models;

namespace TaskFlow.Web.Models.Boards;

public class BoardListViewModel
{
    public Guid ProjectId { get; set; }

    public Guid? TeamId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public bool CanManage { get; set; }

    public bool ShowingArchived { get; set; }

    public IReadOnlyList<BoardListItem> Boards { get; set; }
        = Array.Empty<BoardListItem>();
}
