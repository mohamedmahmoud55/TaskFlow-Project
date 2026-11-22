using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Teams.Models;

public class TeamMemberItem
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public TeamRole Role { get; set; }

    public bool IsActive { get; set; }

    public DateTime JoinedAt { get; set; }

    public bool IsCurrentUser { get; set; }

    // Whether the current user may change this member's role or deactivate
    // them. Decided by the service, so the page never has to guess.
    public bool CanManage { get; set; }
}
