
namespace TaskFlow.Data.Entities;

public class TaskLabel
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public Guid LabelId { get; set; }

    public DateTime AssignedAt { get; set; }

    // Navigation Properties
    public Task Task { get; set; } = null!;

    public Label Label { get; set; } = null!;
}
