using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Projects.Models;

namespace TaskFlow.Services.Projects;

public sealed class ProjectMemberService : IProjectMemberService
{
    private readonly ApplicationDbContext _dbContext;

    public ProjectMemberService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectMemberItem>> GetProjectMembersAsync(
        Guid projectId,
        string userId)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<ProjectMemberItem>();
        }

        var canView = await _dbContext.Projects
            .ReadableBy(userId)
            .AnyAsync(project => project.Id == projectId);

        if (!canView)
        {
            return Array.Empty<ProjectMemberItem>();
        }

        return await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId)
            .OrderByDescending(member => member.IsActive)
            .ThenBy(member => member.User.UserName)
            .Select(member => new ProjectMemberItem
            {
                Id = member.Id,
                UserId = member.UserId,
                DisplayName = member.User.UserName
                    ?? member.User.Email
                    ?? member.UserId,
                Email = member.User.Email ?? string.Empty,
                Role = member.Role,
                IsActive = member.IsActive,
                JoinedAt = member.JoinedAt
            })
            .ToListAsync();
    }

    public async Task<ProjectMemberOperationResult> AddProjectMemberAsync(
        Guid projectId,
        string actorUserId,
        string email,
        ProjectRole role)
    {
        var (canManage, exists) = await CheckManageAccessAsync(projectId, actorUserId);

        if (!exists)
        {
            return ProjectMemberOperationResult.Failure("Project not found.");
        }

        if (!canManage)
        {
            return ProjectMemberOperationResult.Failure(
                "You do not have permission to manage project members.");
        }

        if (!Enum.IsDefined(role))
        {
            return ProjectMemberOperationResult.Failure("Invalid project role.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return ProjectMemberOperationResult.Failure("Please enter a valid email address.");
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail);

        if (targetUser is null)
        {
            return ProjectMemberOperationResult.Failure(
                "No registered account was found with this email.");
        }

        var existingMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(member =>
                member.ProjectId == projectId &&
                member.UserId == targetUser.Id);

        if (existingMembership is not null)
        {
            if (existingMembership.IsActive)
            {
                return ProjectMemberOperationResult.Failure(
                    "This user is already a project member.");
            }

            existingMembership.Role = role;
            existingMembership.IsActive = true;
            existingMembership.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            var membership = new ProjectMember
            {
                ProjectId = projectId,
                UserId = targetUser.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _dbContext.ProjectMembers.Add(membership);
        }

        try
        {
            await _dbContext.SaveChangesAsync();
            return ProjectMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return ProjectMemberOperationResult.Failure(
                "The project member could not be added.");
        }
    }

    public async Task<ProjectMemberOperationResult> UpdateProjectMemberRoleAsync(
        Guid projectId,
        Guid membershipId,
        string actorUserId,
        ProjectRole role)
    {
        var (canManage, exists) = await CheckManageAccessAsync(projectId, actorUserId);

        if (!exists)
        {
            return ProjectMemberOperationResult.Failure("Project not found.");
        }

        if (!canManage)
        {
            return ProjectMemberOperationResult.Failure(
                "You do not have permission to manage project members.");
        }

        if (!Enum.IsDefined(role))
        {
            return ProjectMemberOperationResult.Failure("Invalid project role.");
        }

        var membership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.ProjectId == projectId &&
                member.IsActive);

        if (membership is null)
        {
            return ProjectMemberOperationResult.Failure(
                "Active project membership not found.");
        }

        if (membership.UserId == actorUserId &&
            role != ProjectRole.Manager)
        {
            return ProjectMemberOperationResult.Failure(
                "You cannot remove your own Project Manager role.");
        }

        if (membership.Role == ProjectRole.Manager &&
            role != ProjectRole.Manager)
        {
            var managerCount = await _dbContext.ProjectMembers
                .CountAsync(member =>
                    member.ProjectId == projectId &&
                    member.IsActive &&
                    member.Role == ProjectRole.Manager);

            if (managerCount <= 1)
            {
                return ProjectMemberOperationResult.Failure(
                    "The project must have at least one active Manager.");
            }
        }

        membership.Role = role;

        try
        {
            await _dbContext.SaveChangesAsync();
            return ProjectMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return ProjectMemberOperationResult.Failure(
                "The member role could not be updated.");
        }
    }

    public async Task<ProjectMemberOperationResult> DeactivateProjectMemberAsync(
        Guid projectId,
        Guid membershipId,
        string actorUserId)
    {
        var (canManage, exists) = await CheckManageAccessAsync(projectId, actorUserId);

        if (!exists)
        {
            return ProjectMemberOperationResult.Failure("Project not found.");
        }

        if (!canManage)
        {
            return ProjectMemberOperationResult.Failure(
                "You do not have permission to manage project members.");
        }

        var membership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.ProjectId == projectId &&
                member.IsActive);

        if (membership is null)
        {
            return ProjectMemberOperationResult.Failure(
                "Active project membership not found.");
        }

        if (membership.UserId == actorUserId)
        {
            return ProjectMemberOperationResult.Failure(
                "You cannot deactivate your own project membership.");
        }

        if (membership.Role == ProjectRole.Manager)
        {
            var managerCount = await _dbContext.ProjectMembers
                .CountAsync(member =>
                    member.ProjectId == projectId &&
                    member.IsActive &&
                    member.Role == ProjectRole.Manager);

            if (managerCount <= 1)
            {
                return ProjectMemberOperationResult.Failure(
                    "The last active Project Manager cannot be deactivated.");
            }
        }

        membership.IsActive = false;

        try
        {
            await _dbContext.SaveChangesAsync();
            return ProjectMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return ProjectMemberOperationResult.Failure(
                "The project member could not be deactivated.");
        }
    }

    public async Task<bool> CanManageProjectMembersAsync(
        Guid projectId,
        string userId)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await _dbContext.Projects
            .ManageableBy(userId)
            .AnyAsync(project => project.Id == projectId);
    }

    private async Task<(bool CanManage, bool Exists)> CheckManageAccessAsync(
        Guid projectId,
        string actorUserId)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserId))
        {
            return (false, false);
        }

        var projectExists = await _dbContext.Projects
            .AnyAsync(project =>
                project.Id == projectId &&
                !project.IsArchived &&
                !project.Team.IsArchived &&
                !project.Team.Workspace.IsArchived);

        if (!projectExists)
        {
            return (false, false);
        }

        var canManage = await _dbContext.Projects
            .ManageableBy(actorUserId)
            .AnyAsync(project => project.Id == projectId);

        return (canManage, true);
    }
}
