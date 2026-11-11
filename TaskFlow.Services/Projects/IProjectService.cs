using TaskFlow.Data.Enums;
using TaskFlow.Services.Projects.Models;

namespace TaskFlow.Services.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectListItem>> GetProjectsAsync(
        Guid teamId,
        Guid? expectedWorkspaceId,
        string userId);

    Task<ProjectDetails?> GetProjectAsync(
        Guid projectId,
        Guid? expectedTeamId,
        string userId);

    Task<ProjectCreationResult> CreateProjectAsync(
        Guid teamId,
        Guid? expectedWorkspaceId,
        string userId,
        string name,
        string? description);

    Task<IReadOnlyList<ProjectMemberItem>> GetProjectMembersAsync(
        Guid projectId,
        string userId);

    Task<ProjectMemberOperationResult> AddProjectMemberAsync(
        Guid projectId,
        string actorUserId,
        string email,
        ProjectRole role);

    Task<ProjectMemberOperationResult> UpdateProjectMemberRoleAsync(
        Guid projectId,
        Guid membershipId,
        string actorUserId,
        ProjectRole role);

    Task<ProjectMemberOperationResult> DeactivateProjectMemberAsync(
        Guid projectId,
        Guid membershipId,
        string actorUserId);
    Task<bool> CanManageProjectMembersAsync(
    Guid projectId,
    string userId);

}

