using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Workspaces;

/// <summary>
/// Who may give which workspace role to other people.
///
///   Owner  can hand out Admin, Member and Viewer
///   Admin  can hand out Member and Viewer
///   others cannot hand out anything
///
/// Nobody can hand out Owner, and a person can only manage members whose
/// current role they are allowed to hand out, so an Admin cannot touch
/// another Admin or the Owner.
/// </summary>
public static class WorkspaceMemberRules
{
    private static readonly WorkspaceRole[] OwnerCanAssign =
    {
        WorkspaceRole.Admin,
        WorkspaceRole.Member,
        WorkspaceRole.Viewer
    };

    private static readonly WorkspaceRole[] AdminCanAssign =
    {
        WorkspaceRole.Member,
        WorkspaceRole.Viewer
    };

    public static IReadOnlyList<WorkspaceRole> AssignableRoles(WorkspaceRole actorRole)
    {
        return actorRole switch
        {
            WorkspaceRole.Owner => OwnerCanAssign,
            WorkspaceRole.Admin => AdminCanAssign,
            _ => Array.Empty<WorkspaceRole>()
        };
    }

    public static bool CanAssign(WorkspaceRole actorRole, WorkspaceRole role)
    {
        return AssignableRoles(actorRole).Contains(role);
    }
}
