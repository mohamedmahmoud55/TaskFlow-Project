using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class WorkspaceInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public WorkspaceRole InvitedRole { get; set; } = WorkspaceRole.Member;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public string? InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public ApplicationUser? InvitedByUser { get; set; }
}
