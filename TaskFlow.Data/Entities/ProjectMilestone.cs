using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class ProjectMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Planned;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
}
