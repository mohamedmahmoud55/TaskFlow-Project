
using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class TeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Keys
    public Guid TeamId { get; set; }

    public string UserId { get; set; } = string.Empty;

    // Membership Information
    public TeamRole Role { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public Team Team { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
