using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Teams;

/// <summary>
/// Who may change the team itself (as opposed to its members, which is
/// TeamMemberRules).
///
///   edit     workspace Owner or Admin, or a Lead of the team
///   archive  workspace Owner or Admin (also restore)
/// </summary>
public static class TeamRules
{
    public static bool CanEdit(bool isWorkspaceAdmin, TeamRole? teamRole)
    {
        return isWorkspaceAdmin || teamRole == TeamRole.Lead;
    }

    public static bool CanArchive(bool isWorkspaceAdmin)
    {
        return isWorkspaceAdmin;
    }
}
