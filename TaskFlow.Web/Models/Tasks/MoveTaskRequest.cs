namespace TaskFlow.Web.Models.Tasks;

public sealed class MoveTaskRequest
{
    public Guid TaskId { get; set; }

    public Guid TargetColumnId { get; set; }

    public int TargetIndex { get; set; }
}
