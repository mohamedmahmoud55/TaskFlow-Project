using TaskFlow.Services.Tasks.Models;

namespace TaskFlow.Web.Models.Tasks;

public class TaskListViewModel
{
    public Guid BoardColumnId { get; set; }

    public bool CanCreateTasks { get; set; }

    public IReadOnlyList<TaskListItem> Tasks { get; set; }
        = Array.Empty<TaskListItem>();
}
