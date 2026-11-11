
namespace TaskFlow.Data.Entities;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid WorkspaceId { get; set; }

    // Team Information
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsArchived { get; set; } = false;

    // Navigation Property
    public Workspace Workspace { get; set; } = null!;
    public ICollection<TeamMember> Members { get; set; }
    = new List<TeamMember>();
    public ICollection<Project> Projects { get; set; }
    = new List<Project>();
}

