using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Task;

public class UpdateTaskAssigneesViewModel
{
    public Guid TaskId { get; set; }

    public List<string> SelectedUserIds { get; set; } = new();
}