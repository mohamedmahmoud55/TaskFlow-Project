using TaskFlow.Data.Enums;
using TaskFlow.Services.Teams.Models;

namespace TaskFlow.Services.Teams;

public interface ITeamService
{
    Task<IReadOnlyList<TeamListItem>> GetTeamsAsync(Guid workspaceId, string userId);

    Task<TeamDetails?> GetTeamAsync(Guid teamId, Guid? expectedWorkspaceId, string userId);

    Task<TeamCreationResult> CreateTeamAsync(
        Guid workspaceId,
        string userId,
        string name,
        string? description);

    // The team's members. Every active member (and workspace Owners and
    // Admins) can see the active ones; people who can manage the team also
    // see deactivated members.
    Task<IReadOnlyList<TeamMemberItem>> GetTeamMembersAsync(
        Guid teamId,
        string userId);

    // What the user may do with the team's members: whether they can manage
    // them, which roles they can hand out, and who can be added.
    Task<TeamMemberManagement> GetTeamMemberManagementAsync(
        Guid teamId,
        string userId);

    // Adds a workspace member to the team, or reactivates a deactivated one.
    Task<TeamMemberOperationResult> AddTeamMemberAsync(
        Guid teamId,
        string actorUserId,
        string targetUserId,
        TeamRole role);

    Task<TeamMemberOperationResult> UpdateTeamMemberRoleAsync(
        Guid teamId,
        Guid membershipId,
        string actorUserId,
        TeamRole role);

    Task<TeamMemberOperationResult> DeactivateTeamMemberAsync(
        Guid teamId,
        Guid membershipId,
        string actorUserId);

    // What the user may do with the team itself: edit it, archive it.
    Task<TeamLifecycleAccess> GetTeamLifecycleAccessAsync(
        Guid teamId,
        string userId);

    // Archived teams the user could restore (workspace Owners and Admins).
    Task<IReadOnlyList<TeamListItem>> GetArchivedTeamsAsync(
        Guid workspaceId,
        string userId);

    // Workspace Owners and Admins, and the team's Leads, can edit a team.
    Task<TeamOperationResult> UpdateTeamAsync(
        Guid teamId,
        string userId,
        string name,
        string? description);

    // Workspace Owners and Admins can archive. Hides the team and its
    // projects, boards and tasks from everyone until it is restored.
    Task<TeamOperationResult> ArchiveTeamAsync(
        Guid teamId,
        string userId);

    Task<TeamOperationResult> RestoreTeamAsync(
        Guid teamId,
        string userId);
}
