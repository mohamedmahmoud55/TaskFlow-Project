
using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Keys
    public Guid ProjectId { get; set; }

    public string UserId { get; set; } = string.Empty;

    // Membership Information
    public ProjectRole Role { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public Project Project { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}

