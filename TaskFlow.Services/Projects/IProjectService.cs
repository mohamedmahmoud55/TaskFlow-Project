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

    // Whether the user may edit, change the status of, or archive the project.
    Task<bool> CanManageProjectAsync(
        Guid projectId,
        string userId);

    Task<ProjectOperationResult> UpdateProjectAsync(
        Guid projectId,
        string userId,
        string name,
        string? description);

    Task<ProjectOperationResult> UpdateProjectStatusAsync(
        Guid projectId,
        string userId,
        ProjectStatus status);

    // Hides the project (with its boards and tasks) from everyone until it
    // is restored.
    Task<ProjectOperationResult> ArchiveProjectAsync(
        Guid projectId,
        string userId);

    Task<ProjectOperationResult> RestoreProjectAsync(
        Guid projectId,
        string userId);
}

