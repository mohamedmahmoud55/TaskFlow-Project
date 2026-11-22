using TaskFlow.Data.Enums;
using TaskFlow.Services.Workspaces.Models;

namespace TaskFlow.Services.Workspaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceListItem>> GetUserWorkspacesAsync(string userId);

    Task<WorkspaceDetails?> GetWorkspaceAsync(Guid workspaceId, string userId);

    Task<WorkspaceCreationResult> CreateWorkspaceAsync(
        string userId,
        string name,
        string? description);

    // The workspace's members. Every active member can see the active ones;
    // Owners and Admins also see people who were deactivated.
    Task<IReadOnlyList<WorkspaceMemberItem>> GetWorkspaceMembersAsync(
        Guid workspaceId,
        string userId);

    Task<bool> CanManageWorkspaceMembersAsync(
        Guid workspaceId,
        string userId);

    // Adds a registered user by email, or reactivates a deactivated member.
    Task<WorkspaceMemberOperationResult> AddWorkspaceMemberAsync(
        Guid workspaceId,
        string actorUserId,
        string email,
        WorkspaceRole role);

    Task<WorkspaceMemberOperationResult> UpdateWorkspaceMemberRoleAsync(
        Guid workspaceId,
        Guid membershipId,
        string actorUserId,
        WorkspaceRole role);

    Task<WorkspaceMemberOperationResult> DeactivateWorkspaceMemberAsync(
        Guid workspaceId,
        Guid membershipId,
        string actorUserId);

    // Archived workspaces the user could restore (the workspaces they own).
    Task<IReadOnlyList<WorkspaceListItem>> GetArchivedWorkspacesAsync(
        string userId);

    // Owners and Admins can edit a workspace.
    Task<WorkspaceOperationResult> UpdateWorkspaceAsync(
        Guid workspaceId,
        string userId,
        string name,
        string? description);

    // Only the Owner can archive. Hides the workspace, its teams, projects,
    // boards and tasks from everyone until it is restored.
    Task<WorkspaceOperationResult> ArchiveWorkspaceAsync(
        Guid workspaceId,
        string userId);

    Task<WorkspaceOperationResult> RestoreWorkspaceAsync(
        Guid workspaceId,
        string userId);
}
