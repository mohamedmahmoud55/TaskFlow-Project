using TaskFlow.Services.Boards.Models;
using TaskFlow.Services.Tasks.Models;

namespace TaskFlow.Web.Models.Boards;

public class BoardDetailsViewModel
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? TeamId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public bool CanManage { get; set; }

    public IReadOnlyList<BoardColumnItem> Columns { get; set; }
        = Array.Empty<BoardColumnItem>();

    public Dictionary<Guid, IReadOnlyList<TaskListItem>> TasksByColumn { get; set; }
        = new();
}