
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Projects.Models;

namespace TaskFlow.Services.Projects;

public sealed class ProjectService : IProjectService
{
    private readonly ApplicationDbContext _dbContext;

    public ProjectService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectListItem>> GetProjectsAsync(
        Guid teamId,
        Guid? expectedWorkspaceId,
        string userId)
    {
        if (teamId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<ProjectListItem>();
        }

        var workspaceAccess = _dbContext.WorkspaceMembers
            .Where(member =>
                member.UserId == userId &&
                member.IsActive &&
                !member.Workspace.IsArchived &&
                (member.Role == WorkspaceRole.Owner ||
                 member.Role == WorkspaceRole.Admin));

        var projectAccess = _dbContext.ProjectMembers
            .Where(member =>
                member.UserId == userId &&
                member.IsActive &&
                !member.Project.IsArchived &&
                !member.Project.Team.IsArchived &&
                !member.Project.Team.Workspace.IsArchived);

        var query = _dbContext.Projects
            .AsNoTracking()
            .Where(project =>
                project.TeamId == teamId &&
                !project.IsArchived &&
                !project.Team.IsArchived &&
                !project.Team.Workspace.IsArchived);

        if (expectedWorkspaceId.HasValue)
        {
            query = query.Where(project =>
                project.Team.WorkspaceId == expectedWorkspaceId.Value);
        }

        return await query
            .Where(project =>
                workspaceAccess.Any(member =>
                    member.WorkspaceId == project.Team.WorkspaceId) ||
                projectAccess.Any(member =>
                    member.ProjectId == project.Id))
            .OrderBy(project => project.Name)
            .Select(project => new ProjectListItem(
                project.Id,
                project.TeamId,
                project.Team.WorkspaceId,
                project.Name,
                project.Description,
                project.Status,
                project.Members
                    .Where(member =>
                        member.UserId == userId && member.IsActive)
                    .Select(member => (ProjectRole?)member.Role)
                    .FirstOrDefault()))
            .ToListAsync();
    }

    public async Task<ProjectDetails?> GetProjectAsync(
        Guid projectId,
        Guid? expectedTeamId,
        string userId)
    {
        if (projectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var query = _dbContext.Projects
            .AsNoTracking()
            .Where(project =>
                project.Id == projectId &&
                !project.IsArchived &&
                !project.Team.IsArchived &&
                !project.Team.Workspace.IsArchived);

        if (expectedTeamId.HasValue)
        {
            query = query.Where(project =>
                project.TeamId == expectedTeamId.Value);
        }

        return await query
            .Where(project =>
                project.Team.Workspace.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    (member.Role == WorkspaceRole.Owner ||
                     member.Role == WorkspaceRole.Admin)) ||
                project.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive))
            .Select(project => new ProjectDetails(
                project.Id,
                project.TeamId,
                project.Team.WorkspaceId,
                project.Team.Workspace.Name,
                project.Team.Name,
                project.Name,
                project.Description,
                project.Status,
                project.Members
                    .Where(member =>
                        member.UserId == userId && member.IsActive)
                    .Select(member => (ProjectRole?)member.Role)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync();
    }

    public async Task<ProjectCreationResult> CreateProjectAsync(
        Guid teamId,
        Guid? expectedWorkspaceId,
        string userId,
        string name,
        string? description)
    {
        if (teamId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return ProjectCreationResult.Failure(
                "A valid team and authenticated user are required.");
        }

        var trimmedName = name.Trim();

        if (trimmedName.Length == 0 || trimmedName.Length > 200)
        {
            return ProjectCreationResult.Failure(
                "Project name must be between 1 and 200 characters.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalizedDescription?.Length > 2000)
        {
            return ProjectCreationResult.Failure(
                "Project description cannot exceed 2000 characters.");
        }

        var teamQuery = _dbContext.Teams
            .Where(team =>
                team.Id == teamId &&
                !team.IsArchived &&
                !team.Workspace.IsArchived);

        if (expectedWorkspaceId.HasValue)
        {
            teamQuery = teamQuery.Where(team =>
                team.WorkspaceId == expectedWorkspaceId.Value);
        }

        var team = await teamQuery.SingleOrDefaultAsync();

        if (team is null)
        {
            return ProjectCreationResult.Failure(
                "The requested team could not be found.");
        }

        var canCreate = await _dbContext.WorkspaceMembers
            .AnyAsync(member =>
                member.WorkspaceId == team.WorkspaceId &&
                member.UserId == userId &&
                member.IsActive &&
                !member.Workspace.IsArchived &&
                (member.Role == WorkspaceRole.Owner ||
                 member.Role == WorkspaceRole.Admin));

        if (!canCreate)
        {
            canCreate = await _dbContext.TeamMembers
                .AnyAsync(member =>
                    member.TeamId == team.Id &&
                    member.UserId == userId &&
                    member.IsActive &&
                    member.Role == TeamRole.Lead &&
                    !member.Team.IsArchived &&
                    !member.Team.Workspace.IsArchived);
        }

        if (!canCreate)
        {
            return ProjectCreationResult.Failure(
                "You do not have permission to create projects in this team.");
        }

        var project = new Project
        {
            TeamId = team.Id,
            Name = trimmedName,
            Description = normalizedDescription,
            Status = ProjectStatus.Planned,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        var membership = new ProjectMember
        {
            Project = project,
            UserId = userId,
            Role = ProjectRole.Manager,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Projects.Add(project);
        _dbContext.ProjectMembers.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync();
            return ProjectCreationResult.Success(project.Id);
        }
        catch (DbUpdateException)
        {
            return ProjectCreationResult.Failure(
                "The project could not be created.");
        }
    }

    // Gets project members.
    public async Task<IReadOnlyList<ProjectMemberItem>>
        GetProjectMembersAsync(Guid projectId, string userId)
    {
        var project = await GetActiveProjectAsync(projectId);

        if (project is null)
            return Array.Empty<ProjectMemberItem>();

        var canView =
            await IsWorkspaceAdminAsync(project.Team.WorkspaceId, userId) ||
            await _dbContext.ProjectMembers.AnyAsync(member =>
                member.ProjectId == projectId &&
                member.UserId == userId &&
                member.IsActive);

        if (!canView)
            return Array.Empty<ProjectMemberItem>();

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

    // Adds a new member or reactivates an existing membership.
    public async Task<ProjectMemberOperationResult>
        AddProjectMemberAsync(
            Guid projectId,
            string actorUserId,
            string email,
            ProjectRole role)
    {
        var project = await GetActiveProjectAsync(projectId);

        if (project is null)
            return ProjectMemberOperationResult.Failure(
                "Project not found.");

        if (!await CanManageMembersAsync(project, actorUserId))
            return ProjectMemberOperationResult.Failure(
                "You do not have permission to manage project members.");

        if (!Enum.IsDefined(role))
            return ProjectMemberOperationResult.Failure(
                "Invalid project role.");

        if (string.IsNullOrWhiteSpace(email))
            return ProjectMemberOperationResult.Failure(
                "Please enter a valid email address.");

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(user =>
                user.NormalizedEmail == normalizedEmail);

        if (targetUser is null)
            return ProjectMemberOperationResult.Failure(
                "No registered account was found with this email.");

        var existingMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(member =>
                member.ProjectId == projectId &&
                member.UserId == targetUser.Id);

        if (existingMembership is not null)
        {
            if (existingMembership.IsActive)
                return ProjectMemberOperationResult.Failure(
                    "This user is already a project member.");

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

    // Changes an existing member's role.
    public async Task<ProjectMemberOperationResult>
        UpdateProjectMemberRoleAsync(
            Guid projectId,
            Guid membershipId,
            string actorUserId,
            ProjectRole role)
    {
        var project = await GetActiveProjectAsync(projectId);

        if (project is null)
            return ProjectMemberOperationResult.Failure(
                "Project not found.");

        if (!await CanManageMembersAsync(project, actorUserId))
            return ProjectMemberOperationResult.Failure(
                "You do not have permission to manage project members.");

        if (!Enum.IsDefined(role))
            return ProjectMemberOperationResult.Failure(
                "Invalid project role.");

        var membership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.ProjectId == projectId &&
                member.IsActive);

        if (membership is null)
            return ProjectMemberOperationResult.Failure(
                "Active project membership not found.");

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

    // Deactivates a project membership.
    public async Task<ProjectMemberOperationResult>
        DeactivateProjectMemberAsync(
            Guid projectId,
            Guid membershipId,
            string actorUserId)
    {
        var project = await GetActiveProjectAsync(projectId);

        if (project is null)
            return ProjectMemberOperationResult.Failure(
                "Project not found.");

        if (!await CanManageMembersAsync(project, actorUserId))
            return ProjectMemberOperationResult.Failure(
                "You do not have permission to manage project members.");

        var membership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.ProjectId == projectId &&
                member.IsActive);

        if (membership is null)
            return ProjectMemberOperationResult.Failure(
                "Active project membership not found.");

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

    // Loads a non-archived project and its workspace.
    private async Task<Project?> GetActiveProjectAsync(Guid projectId)
    {
        if (projectId == Guid.Empty)
            return null;

        return await _dbContext.Projects
            .Include(project => project.Team)
                .ThenInclude(team => team.Workspace)
            .FirstOrDefaultAsync(project =>
                project.Id == projectId &&
                !project.IsArchived &&
                !project.Team.IsArchived &&
                !project.Team.Workspace.IsArchived);
    }

    // Checks Workspace Owner/Admin privileges.
    private async Task<bool> IsWorkspaceAdminAsync(
        Guid workspaceId,
        string userId)
    {
        return await _dbContext.WorkspaceMembers.AnyAsync(member =>
            member.WorkspaceId == workspaceId &&
            member.UserId == userId &&
            member.IsActive &&
            (member.Role == WorkspaceRole.Owner ||
             member.Role == WorkspaceRole.Admin));
    }

    // Checks whether the current user can manage project memberships.
    private async Task<bool> CanManageMembersAsync(
        Project project,
        string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return false;

        return await IsWorkspaceAdminAsync(
                   project.Team.WorkspaceId,
                   actorUserId)
               ||
               await _dbContext.ProjectMembers.AnyAsync(member =>
                   member.ProjectId == project.Id &&
                   member.UserId == actorUserId &&
                   member.IsActive &&
                   member.Role == ProjectRole.Manager);
    }
    public async Task<bool> CanManageProjectMembersAsync(
    Guid projectId,
    string userId)
    {
        var project = await GetActiveProjectAsync(projectId);

        if (project is null || string.IsNullOrWhiteSpace(userId))
            return false;

        return await CanManageMembersAsync(project, userId);
    }
}
