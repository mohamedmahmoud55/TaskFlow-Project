
using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class WorkspaceMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Keys
    public Guid WorkspaceId { get; set; }

    public string UserId { get; set; } = string.Empty;

    // Membership Information
    public WorkspaceRole Role { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public Workspace Workspace { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
