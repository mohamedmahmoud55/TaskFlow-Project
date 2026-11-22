using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Workspaces;

/// <summary>
/// Who may change the workspace itself (as opposed to its members, which is
/// WorkspaceMemberRules).
///
///   edit     workspace Owner or Admin
///   archive  workspace Owner only (also restore)
/// </summary>
public static class WorkspaceRules
{
    public static bool CanEdit(WorkspaceRole role)
    {
        return role is WorkspaceRole.Owner or WorkspaceRole.Admin;
    }

    public static bool CanArchive(WorkspaceRole role)
    {
        return role == WorkspaceRole.Owner;
    }
}
