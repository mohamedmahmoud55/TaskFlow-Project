using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Workspaces.Models;

namespace TaskFlow.Services.Workspaces;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly ApplicationDbContext _dbContext;

    public WorkspaceService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WorkspaceListItem>> GetUserWorkspacesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<WorkspaceListItem>();
        }

        return await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.UserId == userId &&
                member.IsActive &&
                !member.Workspace.IsArchived)
            .OrderBy(member => member.Workspace.Name)
            .Select(member => new WorkspaceListItem(
                member.WorkspaceId,
                member.Workspace.Name,
                member.Workspace.Description,
                member.Role))
            .ToListAsync();
    }

    public async Task<WorkspaceDetails?> GetWorkspaceAsync(Guid workspaceId, string userId)
    {
        if (workspaceId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == userId &&
                member.IsActive &&
                !member.Workspace.IsArchived)
            .Select(member => new WorkspaceDetails(
                member.WorkspaceId,
                member.Workspace.Name,
                member.Workspace.Description,
                member.Role))
            .SingleOrDefaultAsync();
    }

    public async Task<WorkspaceCreationResult> CreateWorkspaceAsync(
        string userId,
        string name,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return WorkspaceCreationResult.Failure("A valid authenticated user is required.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length == 0 || trimmedName.Length > 150)
        {
            return WorkspaceCreationResult.Failure("Workspace name must be between 1 and 150 characters.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        if (normalizedDescription?.Length > 1000)
        {
            return WorkspaceCreationResult.Failure("Workspace description cannot exceed 1000 characters.");
        }

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId);
        if (!userExists)
        {
            return WorkspaceCreationResult.Failure("The authenticated user could not be found.");
        }

        var workspace = new Workspace
        {
            Name = trimmedName,
            Description = normalizedDescription,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        var membership = new WorkspaceMember
        {
            Workspace = workspace,
            UserId = userId,
            Role = WorkspaceRole.Owner,
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Workspaces.Add(workspace);
        _dbContext.WorkspaceMembers.Add(membership);

        try
        {
            await _dbContext.SaveChangesAsync();
            return WorkspaceCreationResult.Success(workspace.Id);
        }
        catch (DbUpdateException)
        {
            return WorkspaceCreationResult.Failure("The workspace could not be created.");
        }
    }
}
