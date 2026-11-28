using TaskFlow.Services.Tasks.Models;

namespace TaskFlow.Web.Models.Tasks;

public class TaskListViewModel
{
    public Guid BoardColumnId { get; set; }

    public Guid BoardId { get; set; }

    public string BoardName { get; set; } = string.Empty;

    public string ColumnName { get; set; } = string.Empty;

    public bool CanCreateTasks { get; set; }

    public IReadOnlyList<TaskListItem> Tasks { get; set; }
        = Array.Empty<TaskListItem>();
}
