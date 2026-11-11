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

        var trimmedName = name.Trim();
        if (trimmedName.Length == 0 || trimmedName.Length > 150)
        {
            return TeamCreationResult.Failure("Team name must be between 1 and 150 characters.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        if (normalizedDescription?.Length > 1000)
        {
            return TeamCreationResult.Failure("Team description cannot exceed 1000 characters.");
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
}
