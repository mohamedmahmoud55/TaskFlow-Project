
using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid TeamId { get; set; }

    // Project Information
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsArchived { get; set; } = false;

    // Navigation Property
    public Team Team { get; set; } = null!;
    public ICollection<ProjectMember> Members { get; set; }
    = new List<ProjectMember>();
    public ICollection<Board> Boards { get; set; }
    = new List<Board>();
}
