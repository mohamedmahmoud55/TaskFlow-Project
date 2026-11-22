using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Teams;

/// <summary>
/// Who may give which team role to other people.
///
///   workspace Owner or Admin   can hand out Lead, Member and Viewer
///   team Lead                  can hand out Member and Viewer
///   everyone else              cannot hand out anything
///
/// A person can only manage team members whose current role they are
/// allowed to hand out, so a Lead cannot touch another Lead.
/// </summary>
public static class TeamMemberRules
{
    private static readonly TeamRole[] AdminCanAssign =
    {
        TeamRole.Lead,
        TeamRole.Member,
        TeamRole.Viewer
    };

    private static readonly TeamRole[] LeadCanAssign =
    {
        TeamRole.Member,
        TeamRole.Viewer
    };

    public static IReadOnlyList<TeamRole> AssignableRoles(
        bool isWorkspaceAdmin,
        TeamRole? actorTeamRole)
    {
        if (isWorkspaceAdmin)
        {
            return AdminCanAssign;
        }

        if (actorTeamRole == TeamRole.Lead)
        {
            return LeadCanAssign;
        }

        return Array.Empty<TeamRole>();
    }

    public static bool CanAssign(
        bool isWorkspaceAdmin,
        TeamRole? actorTeamRole,
        TeamRole role)
    {
        return AssignableRoles(isWorkspaceAdmin, actorTeamRole).Contains(role);
    }
}
