using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Teams.Models;

// What the current user may do with a team's members.
public sealed record TeamMemberManagement(
    bool CanManage,
    IReadOnlyList<TeamRole> AssignableRoles,
    IReadOnlyList<TeamMemberCandidate> Candidates)
{
    public static TeamMemberManagement None { get; } = new(
        false,
        Array.Empty<TeamRole>(),
        Array.Empty<TeamMemberCandidate>());
}
