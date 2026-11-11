
namespace TaskFlow.Data.Entities;

public class Label
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Color { get; set; } = "#808080";

    public DateTime CreatedAt { get; set; }

    // Navigation Properties
    public Workspace Workspace { get; set; } = null!;

    public ICollection<TaskLabel> TaskLabels { get; set; }
        = new List<TaskLabel>();
}
