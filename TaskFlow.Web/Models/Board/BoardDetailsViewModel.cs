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

    // Write access (Manager, Member, Workspace Owner/Admin) allows creating
    // and moving tasks across columns; Viewers cannot.
    public bool CanMoveTasks { get; set; }

    public IReadOnlyList<BoardColumnItem> Columns { get; set; }
        = Array.Empty<BoardColumnItem>();

    public Dictionary<Guid, IReadOnlyList<TaskListItem>> TasksByColumn { get; set; }
        = new();

    // The columns the current user may add tasks to.
    public HashSet<Guid> ColumnsOpenToNewTasks { get; set; } = new();

    public bool CanAddTask(Guid columnId) => ColumnsOpenToNewTasks.Contains(columnId);
}
