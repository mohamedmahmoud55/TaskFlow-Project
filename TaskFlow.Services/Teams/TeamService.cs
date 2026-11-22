using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Teams.Models;

namespace TaskFlow.Services.Teams;

public sealed class TeamService : ITeamService
{
    private readonly ApplicationDbContext _dbContext;

    public TeamService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TeamListItem>> GetTeamsAsync(Guid workspaceId, string userId)
    {
        if (workspaceId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<TeamListItem>();
        }

        var workspaceAccess = _dbContext.WorkspaceMembers
            .Where(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == userId &&
                member.IsActive &&
                !member.Workspace.IsArchived);

        var teamAccess = _dbContext.TeamMembers
            .Where(member =>
                member.UserId == userId &&
                member.IsActive &&
                member.Team.WorkspaceId == workspaceId &&
                !member.Team.IsArchived &&
                !member.Team.Workspace.IsArchived);

        return await _dbContext.Teams
            .AsNoTracking()
            .Where(team =>
                team.WorkspaceId == workspaceId &&
                !team.IsArchived &&
                (workspaceAccess.Any(member =>
                    member.Role == WorkspaceRole.Owner || member.Role == WorkspaceRole.Admin) ||
                 teamAccess.Any(member => member.TeamId == team.Id)))
            .OrderBy(team => team.Name)
            .Select(team => new TeamListItem(
                team.Id,
                team.WorkspaceId,
                team.Name,
                team.Description,
                team.Members
                    .Where(member => member.UserId == userId && member.IsActive)
                    .Select(member => (TeamRole?)member.Role)
                    .FirstOrDefault()))
            .ToListAsync();
    }

    public async Task<TeamDetails?> GetTeamAsync(
        Guid teamId,
        Guid? expectedWorkspaceId,
        string userId)
    {
        if (teamId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var query = _dbContext.Teams
            .AsNoTracking()
            .Where(team =>
                team.Id == teamId &&
                !team.IsArchived &&
                !team.Workspace.IsArchived);

        if (expectedWorkspaceId.HasValue)
        {
            query = query.Where(team => team.WorkspaceId == expectedWorkspaceId.Value);
        }

        return await query
            .Where(team =>
                team.Workspace.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    (member.Role == WorkspaceRole.Owner || member.Role == WorkspaceRole.Admin)) ||
                team.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive))
            .Select(team => new TeamDetails(
                team.Id,
                team.WorkspaceId,
                team.Workspace.Name,
                team.Name,
                team.Description,
                team.Members
                    .Where(member => member.UserId == userId && member.IsActive)
                    .Select(member => (TeamRole?)member.Role)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync();
    }

    public async Task<TeamCreationResult> CreateTeamAsync(
        Guid workspaceId,
        string userId,
        string name,
        string? description)
    {
        if (workspaceId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return TeamCreationResult.Failure("A valid workspace and authenticated user are required.");
        }

        var validationError = ValidateTeamInput(
            name,
            description,
            out var trimmedName,
            out var normalizedDescription);

        if (validationError is not null)
        {
            return TeamCreationResult.Failure(validationError);
        }

        var canCreate = await _dbContext.WorkspaceMembers
            .AnyAsync(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == userId &&
                member.IsActive &&
                !member.Workspace.IsArchived &&
                (member.Role == WorkspaceRole.Owner || member.Role == WorkspaceRole.Admin));
        if (!canCreate)
        {
            return TeamCreationResult.Failure("You do not have permission to create teams in this workspace.");
        }

        var team = new Team
        {
            WorkspaceId = workspaceId,
            Name = trimmedName,
            Description = normalizedDescription,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        var membership = new TeamMember
        {
            Team = team,
            UserId = userId,
            Role = TeamRole.Lead,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Teams.Add(team);
        _dbContext.TeamMembers.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync();
            return TeamCreationResult.Success(team.Id);
        }
        catch (DbUpdateException)
        {
            return TeamCreationResult.Failure("The team could not be created.");
        }
    }

    // ---------------------------------------------------------
    // Edit, archive and restore
    //
    // Workspace Owners and Admins, and the team's Leads, can edit a team.
    // Only workspace Owners and Admins can archive and restore it. An
    // archived team is hidden from everyone, with its projects, boards and
    // tasks, because every query requires an unarchived team. Nothing is
    // deleted and memberships stay. The rules live in TeamRules.
    // ---------------------------------------------------------

    public async Task<TeamLifecycleAccess> GetTeamLifecycleAccessAsync(
        Guid teamId,
        string userId)
    {
        var actor = await GetActorAsync(teamId, userId);

        if (actor is null)
        {
            return TeamLifecycleAccess.None;
        }

        return new TeamLifecycleAccess(
            TeamRules.CanEdit(actor.IsWorkspaceAdmin, actor.Role),
            TeamRules.CanArchive(actor.IsWorkspaceAdmin));
    }

    public async Task<IReadOnlyList<TeamListItem>> GetArchivedTeamsAsync(
        Guid workspaceId,
        string userId)
    {
        if (workspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<TeamListItem>();
        }

        return await _dbContext.Teams
            .AsNoTracking()
            .Where(team =>
                team.WorkspaceId == workspaceId &&
                team.IsArchived &&
                !team.Workspace.IsArchived &&
                team.Workspace.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    (member.Role == WorkspaceRole.Owner ||
                     member.Role == WorkspaceRole.Admin)))
            .OrderBy(team => team.Name)
            .Select(team => new TeamListItem(
                team.Id,
                team.WorkspaceId,
                team.Name,
                team.Description,
                null))
            .ToListAsync();
    }

    public async Task<TeamOperationResult> UpdateTeamAsync(
        Guid teamId,
        string userId,
        string name,
        string? description)
    {
        var (team, error) = await LoadTeamForChangeAsync(
            teamId,
            userId,
            actor => TeamRules.CanEdit(actor.IsWorkspaceAdmin, actor.Role),
            "You do not have permission to change this team.");

        if (team is null)
        {
            return TeamOperationResult.Failure(error ?? "Team not found.");
        }

        var validationError = ValidateTeamInput(
            name,
            description,
            out var trimmedName,
            out var normalizedDescription);

        if (validationError is not null)
        {
            return TeamOperationResult.Failure(validationError);
        }

        team.Name = trimmedName;
        team.Description = normalizedDescription;
        team.UpdatedAt = DateTime.UtcNow;

        return await SaveTeamAsync("The team could not be updated.");
    }

    public async Task<TeamOperationResult> ArchiveTeamAsync(
        Guid teamId,
        string userId)
    {
        var (team, error) = await LoadTeamForChangeAsync(
            teamId,
            userId,
            actor => TeamRules.CanArchive(actor.IsWorkspaceAdmin),
            "Only a workspace Owner or Admin can archive a team.");

        if (team is null)
        {
            return TeamOperationResult.Failure(error ?? "Team not found.");
        }

        team.IsArchived = true;
        team.UpdatedAt = DateTime.UtcNow;

        return await SaveTeamAsync("The team could not be archived.");
    }

    public async Task<TeamOperationResult> RestoreTeamAsync(
        Guid teamId,
        string userId)
    {
        if (teamId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return TeamOperationResult.Failure("Archived team not found.");
        }

        // The workspace must be unarchived: restore the workspace first.
        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == teamId &&
                candidate.IsArchived &&
                !candidate.Workspace.IsArchived &&
                candidate.Workspace.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    (member.Role == WorkspaceRole.Owner ||
                     member.Role == WorkspaceRole.Admin)));

        if (team is null)
        {
            return TeamOperationResult.Failure("Archived team not found.");
        }

        team.IsArchived = false;
        team.UpdatedAt = DateTime.UtcNow;

        return await SaveTeamAsync("The team could not be restored.");
    }

    // Loads an unarchived team the user may change, or says why not: people
    // who can see the team are told they lack the right; everyone else is
    // told the team does not exist.
    private async Task<(Team? Team, string? Error)> LoadTeamForChangeAsync(
        Guid teamId,
        string userId,
        Func<TeamActor, bool> isAllowed,
        string refusal)
    {
        var actor = await GetActorAsync(teamId, userId);

        if (actor is null)
        {
            return (null, "Team not found.");
        }

        if (!isAllowed(actor))
        {
            return (null, refusal);
        }

        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(candidate => candidate.Id == teamId);

        return team is null
            ? (null, "Team not found.")
            : (team, null);
    }

    private async Task<TeamOperationResult> SaveTeamAsync(string failureMessage)
    {
        try
        {
            await _dbContext.SaveChangesAsync();

            return TeamOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return TeamOperationResult.Failure(failureMessage);
        }
    }

    private static string? ValidateTeamInput(
        string? name,
        string? description,
        out string trimmedName,
        out string? normalizedDescription)
    {
        trimmedName = name?.Trim() ?? string.Empty;

        normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (trimmedName.Length == 0 || trimmedName.Length > 150)
        {
            return "Team name must be between 1 and 150 characters.";
        }

        if (normalizedDescription?.Length > 1000)
        {
            return "Team description cannot exceed 1000 characters.";
        }

        return null;
    }

    // ---------------------------------------------------------
    // Members
    //
    // Workspace Owners and Admins can manage everyone on a team and can
    // make someone a Lead. A team Lead can manage Members and Viewers only.
    // Nobody can change their own role or deactivate themselves. A person
    // can only be added if they already belong to the team's workspace.
    // The role rules live in TeamMemberRules.
    // ---------------------------------------------------------

    private sealed record TeamActor(
        Guid WorkspaceId,
        bool IsWorkspaceAdmin,
        TeamRole? Role);

    public async Task<IReadOnlyList<TeamMemberItem>> GetTeamMembersAsync(
        Guid teamId,
        string userId)
    {
        var actor = await GetActorAsync(teamId, userId);

        if (actor is null)
        {
            return Array.Empty<TeamMemberItem>();
        }

        var canManage = TeamMemberRules
            .AssignableRoles(actor.IsWorkspaceAdmin, actor.Role)
            .Count > 0;

        var query = _dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.TeamId == teamId);

        if (!canManage)
        {
            query = query.Where(member => member.IsActive);
        }

        var members = await query
            .OrderByDescending(member => member.IsActive)
            .ThenBy(member => member.Role)
            .ThenBy(member => member.User.UserName)
            .Select(member => new TeamMemberItem
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

        foreach (var member in members)
        {
            member.IsCurrentUser = member.UserId == userId;

            member.CanManage =
                member.IsActive &&
                !member.IsCurrentUser &&
                TeamMemberRules.CanAssign(
                    actor.IsWorkspaceAdmin,
                    actor.Role,
                    member.Role);
        }

        return members;
    }

    public async Task<TeamMemberManagement> GetTeamMemberManagementAsync(
        Guid teamId,
        string userId)
    {
        var actor = await GetActorAsync(teamId, userId);

        if (actor is null)
        {
            return TeamMemberManagement.None;
        }

        var assignable = TeamMemberRules.AssignableRoles(
            actor.IsWorkspaceAdmin,
            actor.Role);

        if (assignable.Count == 0)
        {
            return TeamMemberManagement.None;
        }

        // Active workspace members who are not already active on the team.
        // Deactivated team members are included, so they can be added back.
        var candidates = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.WorkspaceId == actor.WorkspaceId &&
                member.IsActive &&
                !_dbContext.TeamMembers.Any(teamMember =>
                    teamMember.TeamId == teamId &&
                    teamMember.UserId == member.UserId &&
                    teamMember.IsActive))
            .OrderBy(member => member.User.UserName)
            .Select(member => new TeamMemberCandidate(
                member.UserId,
                member.User.UserName ?? member.User.Email ?? member.UserId,
                member.User.Email ?? string.Empty))
            .ToListAsync();

        return new TeamMemberManagement(true, assignable, candidates);
    }

    public async Task<TeamMemberOperationResult> AddTeamMemberAsync(
        Guid teamId,
        string actorUserId,
        string targetUserId,
        TeamRole role)
    {
        var actor = await GetActorAsync(teamId, actorUserId);

        if (actor is null)
        {
            return TeamMemberOperationResult.Failure("Team not found.");
        }

        if (TeamMemberRules.AssignableRoles(actor.IsWorkspaceAdmin, actor.Role).Count == 0)
        {
            return TeamMemberOperationResult.Failure(
                "You do not have permission to manage team members.");
        }

        var roleError = CheckAssignableRole(actor, role);

        if (roleError is not null)
        {
            return TeamMemberOperationResult.Failure(roleError);
        }

        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return TeamMemberOperationResult.Failure("Please choose a person to add.");
        }

        var isWorkspaceMember = await _dbContext.WorkspaceMembers
            .AnyAsync(member =>
                member.WorkspaceId == actor.WorkspaceId &&
                member.UserId == targetUserId &&
                member.IsActive);

        if (!isWorkspaceMember)
        {
            return TeamMemberOperationResult.Failure(
                "This person is not a member of the workspace. Add them to the workspace first.");
        }

        var existingMembership = await _dbContext.TeamMembers
            .FirstOrDefaultAsync(member =>
                member.TeamId == teamId &&
                member.UserId == targetUserId);

        if (existingMembership is not null)
        {
            if (existingMembership.IsActive)
            {
                return TeamMemberOperationResult.Failure(
                    "This user is already a team member.");
            }

            existingMembership.Role = role;
            existingMembership.IsActive = true;
            existingMembership.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.TeamMembers.Add(new TeamMember
            {
                TeamId = teamId,
                UserId = targetUserId,
                Role = role,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync();

            return TeamMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return TeamMemberOperationResult.Failure(
                "The team member could not be added.");
        }
    }

    public async Task<TeamMemberOperationResult> UpdateTeamMemberRoleAsync(
        Guid teamId,
        Guid membershipId,
        string actorUserId,
        TeamRole role)
    {
        var actor = await GetActorAsync(teamId, actorUserId);

        if (actor is null)
        {
            return TeamMemberOperationResult.Failure("Team not found.");
        }

        if (TeamMemberRules.AssignableRoles(actor.IsWorkspaceAdmin, actor.Role).Count == 0)
        {
            return TeamMemberOperationResult.Failure(
                "You do not have permission to manage team members.");
        }

        var roleError = CheckAssignableRole(actor, role);

        if (roleError is not null)
        {
            return TeamMemberOperationResult.Failure(roleError);
        }

        var membership = await _dbContext.TeamMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.TeamId == teamId &&
                member.IsActive);

        if (membership is null)
        {
            return TeamMemberOperationResult.Failure(
                "Active team membership not found.");
        }

        var memberError = CheckManageableMember(
            actor,
            membership,
            actorUserId,
            "You cannot change your own team role.");

        if (memberError is not null)
        {
            return TeamMemberOperationResult.Failure(memberError);
        }

        membership.Role = role;

        try
        {
            await _dbContext.SaveChangesAsync();

            return TeamMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return TeamMemberOperationResult.Failure(
                "The member role could not be updated.");
        }
    }

    public async Task<TeamMemberOperationResult> DeactivateTeamMemberAsync(
        Guid teamId,
        Guid membershipId,
        string actorUserId)
    {
        var actor = await GetActorAsync(teamId, actorUserId);

        if (actor is null)
        {
            return TeamMemberOperationResult.Failure("Team not found.");
        }

        if (TeamMemberRules.AssignableRoles(actor.IsWorkspaceAdmin, actor.Role).Count == 0)
        {
            return TeamMemberOperationResult.Failure(
                "You do not have permission to manage team members.");
        }

        var membership = await _dbContext.TeamMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.TeamId == teamId &&
                member.IsActive);

        if (membership is null)
        {
            return TeamMemberOperationResult.Failure(
                "Active team membership not found.");
        }

        var memberError = CheckManageableMember(
            actor,
            membership,
            actorUserId,
            "You cannot deactivate your own team membership.");

        if (memberError is not null)
        {
            return TeamMemberOperationResult.Failure(memberError);
        }

        membership.IsActive = false;

        try
        {
            await _dbContext.SaveChangesAsync();

            return TeamMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return TeamMemberOperationResult.Failure(
                "The team member could not be deactivated.");
        }
    }

    // Who the user is in relation to the team: a workspace Owner or Admin,
    // an active team member, or both. Null when they are neither, or when
    // the team or its workspace is archived.
    private async Task<TeamActor?> GetActorAsync(Guid teamId, string userId)
    {
        if (teamId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var actor = await _dbContext.Teams
            .AsNoTracking()
            .Where(team =>
                team.Id == teamId &&
                !team.IsArchived &&
                !team.Workspace.IsArchived)
            .Select(team => new
            {
                team.WorkspaceId,
                IsWorkspaceAdmin = team.Workspace.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    (member.Role == WorkspaceRole.Owner ||
                     member.Role == WorkspaceRole.Admin)),
                Role = team.Members
                    .Where(member => member.UserId == userId && member.IsActive)
                    .Select(member => (TeamRole?)member.Role)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync();

        if (actor is null || (!actor.IsWorkspaceAdmin && actor.Role is null))
        {
            return null;
        }

        return new TeamActor(actor.WorkspaceId, actor.IsWorkspaceAdmin, actor.Role);
    }

    private static string? CheckAssignableRole(TeamActor actor, TeamRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return "Invalid team role.";
        }

        if (!TeamMemberRules.CanAssign(actor.IsWorkspaceAdmin, actor.Role, role))
        {
            return "Only a workspace Owner or Admin can make someone a Team Lead.";
        }

        return null;
    }

    private static string? CheckManageableMember(
        TeamActor actor,
        TeamMember membership,
        string actorUserId,
        string selfMessage)
    {
        if (membership.UserId == actorUserId)
        {
            return selfMessage;
        }

        if (!TeamMemberRules.CanAssign(actor.IsWorkspaceAdmin, actor.Role, membership.Role))
        {
            return "You do not have permission to manage this member.";
        }

        return null;
    }
}
