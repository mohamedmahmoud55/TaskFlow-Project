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

        var validationError = ValidateWorkspaceInput(
            name,
            description,
            out var trimmedName,
            out var normalizedDescription);

        if (validationError is not null)
        {
            return WorkspaceCreationResult.Failure(validationError);
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

    // ---------------------------------------------------------
    // Edit, archive and restore
    //
    // Owners and Admins can edit a workspace. Only the Owner can archive and
    // restore it. An archived workspace is hidden from everyone, with its
    // teams, projects, boards and tasks, because every query requires an
    // unarchived workspace. Nothing is deleted and memberships stay.
    // The rules live in WorkspaceRules.
    // ---------------------------------------------------------

    public async Task<IReadOnlyList<WorkspaceListItem>> GetArchivedWorkspacesAsync(
        string userId)
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
                member.Role == WorkspaceRole.Owner &&
                member.Workspace.IsArchived)
            .OrderBy(member => member.Workspace.Name)
            .Select(member => new WorkspaceListItem(
                member.WorkspaceId,
                member.Workspace.Name,
                member.Workspace.Description,
                member.Role))
            .ToListAsync();
    }

    public async Task<WorkspaceOperationResult> UpdateWorkspaceAsync(
        Guid workspaceId,
        string userId,
        string name,
        string? description)
    {
        var (workspace, error) = await LoadWorkspaceForChangeAsync(
            workspaceId,
            userId,
            WorkspaceRules.CanEdit,
            "You do not have permission to change this workspace.");

        if (workspace is null)
        {
            return WorkspaceOperationResult.Failure(error ?? "Workspace not found.");
        }

        var validationError = ValidateWorkspaceInput(
            name,
            description,
            out var trimmedName,
            out var normalizedDescription);

        if (validationError is not null)
        {
            return WorkspaceOperationResult.Failure(validationError);
        }

        workspace.Name = trimmedName;
        workspace.Description = normalizedDescription;
        workspace.UpdatedAt = DateTime.UtcNow;

        return await SaveWorkspaceAsync("The workspace could not be updated.");
    }

    public async Task<WorkspaceOperationResult> ArchiveWorkspaceAsync(
        Guid workspaceId,
        string userId)
    {
        var (workspace, error) = await LoadWorkspaceForChangeAsync(
            workspaceId,
            userId,
            WorkspaceRules.CanArchive,
            "Only the workspace Owner can archive a workspace.");

        if (workspace is null)
        {
            return WorkspaceOperationResult.Failure(error ?? "Workspace not found.");
        }

        workspace.IsArchived = true;
        workspace.UpdatedAt = DateTime.UtcNow;

        return await SaveWorkspaceAsync("The workspace could not be archived.");
    }

    public async Task<WorkspaceOperationResult> RestoreWorkspaceAsync(
        Guid workspaceId,
        string userId)
    {
        if (workspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
        {
            return WorkspaceOperationResult.Failure("Archived workspace not found.");
        }

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == workspaceId &&
                candidate.IsArchived &&
                candidate.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    member.Role == WorkspaceRole.Owner));

        if (workspace is null)
        {
            return WorkspaceOperationResult.Failure("Archived workspace not found.");
        }

        workspace.IsArchived = false;
        workspace.UpdatedAt = DateTime.UtcNow;

        return await SaveWorkspaceAsync("The workspace could not be restored.");
    }

    // Loads an unarchived workspace the user may change, or says why not:
    // members who lack the right are told so; everyone else is told the
    // workspace does not exist.
    private async Task<(Workspace? Workspace, string? Error)> LoadWorkspaceForChangeAsync(
        Guid workspaceId,
        string userId,
        Func<WorkspaceRole, bool> isAllowed,
        string refusal)
    {
        var role = await GetActiveRoleAsync(workspaceId, userId);

        if (role is null)
        {
            return (null, "Workspace not found.");
        }

        if (!isAllowed(role.Value))
        {
            return (null, refusal);
        }

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(candidate => candidate.Id == workspaceId);

        return workspace is null
            ? (null, "Workspace not found.")
            : (workspace, null);
    }

    private async Task<WorkspaceOperationResult> SaveWorkspaceAsync(string failureMessage)
    {
        try
        {
            await _dbContext.SaveChangesAsync();

            return WorkspaceOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return WorkspaceOperationResult.Failure(failureMessage);
        }
    }

    private static string? ValidateWorkspaceInput(
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
            return "Workspace name must be between 1 and 150 characters.";
        }

        if (normalizedDescription?.Length > 1000)
        {
            return "Workspace description cannot exceed 1000 characters.";
        }

        return null;
    }

    // ---------------------------------------------------------
    // Members
    //
    // Owners can hand out Admin, Member and Viewer. Admins can hand out
    // Member and Viewer. Nobody can create, change or remove an Owner here,
    // and nobody can change their own role or remove themselves. The role
    // hierarchy itself lives in WorkspaceMemberRules.
    // ---------------------------------------------------------

    public async Task<IReadOnlyList<WorkspaceMemberItem>> GetWorkspaceMembersAsync(
        Guid workspaceId,
        string userId)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, userId);

        if (actorRole is null)
        {
            return Array.Empty<WorkspaceMemberItem>();
        }

        var canManage = WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count > 0;

        var query = _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId);

        if (!canManage)
        {
            query = query.Where(member => member.IsActive);
        }

        var members = await query
            .OrderByDescending(member => member.IsActive)
            .ThenBy(member => member.Role)
            .ThenBy(member => member.User.UserName)
            .Select(member => new WorkspaceMemberItem
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
                WorkspaceMemberRules.CanAssign(actorRole.Value, member.Role);
        }

        return members;
    }

    public async Task<bool> CanManageWorkspaceMembersAsync(
        Guid workspaceId,
        string userId)
    {
        var role = await GetActiveRoleAsync(workspaceId, userId);

        return role is not null &&
            WorkspaceMemberRules.AssignableRoles(role.Value).Count > 0;
    }

    public async Task<WorkspaceMemberOperationResult> AddWorkspaceMemberAsync(
        Guid workspaceId,
        string actorUserId,
        string email,
        WorkspaceRole role)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, actorUserId);

        if (actorRole is null)
        {
            return WorkspaceMemberOperationResult.Failure("Workspace not found.");
        }

        if (WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count == 0)
        {
            return WorkspaceMemberOperationResult.Failure(
                "You do not have permission to manage workspace members.");
        }

        var roleError = CheckAssignableRole(actorRole.Value, role);

        if (roleError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(roleError);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return WorkspaceMemberOperationResult.Failure(
                "Please enter a valid email address.");
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail);

        if (targetUser is null)
        {
            return WorkspaceMemberOperationResult.Failure(
                "No registered account was found with this email.");
        }

        var existingMembership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == targetUser.Id);

        if (existingMembership is not null)
        {
            if (existingMembership.Role == WorkspaceRole.Owner)
            {
                return WorkspaceMemberOperationResult.Failure(
                    "The workspace Owner cannot be changed here.");
            }

            if (existingMembership.IsActive)
            {
                return WorkspaceMemberOperationResult.Failure(
                    "This user is already a workspace member.");
            }

            existingMembership.Role = role;
            existingMembership.IsActive = true;
            existingMembership.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = targetUser.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync();

            return WorkspaceMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return WorkspaceMemberOperationResult.Failure(
                "The workspace member could not be added.");
        }
    }

    public async Task<WorkspaceMemberOperationResult> UpdateWorkspaceMemberRoleAsync(
        Guid workspaceId,
        Guid membershipId,
        string actorUserId,
        WorkspaceRole role)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, actorUserId);

        if (actorRole is null)
        {
            return WorkspaceMemberOperationResult.Failure("Workspace not found.");
        }

        if (WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count == 0)
        {
            return WorkspaceMemberOperationResult.Failure(
                "You do not have permission to manage workspace members.");
        }

        var roleError = CheckAssignableRole(actorRole.Value, role);

        if (roleError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(roleError);
        }

        var membership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.WorkspaceId == workspaceId &&
                member.IsActive);

        if (membership is null)
        {
            return WorkspaceMemberOperationResult.Failure(
                "Active workspace membership not found.");
        }

        var memberError = CheckManageableMember(
            actorRole.Value,
            membership,
            actorUserId,
            "You cannot change your own workspace role.");

        if (memberError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(memberError);
        }

        membership.Role = role;

        try
        {
            await _dbContext.SaveChangesAsync();

            return WorkspaceMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return WorkspaceMemberOperationResult.Failure(
                "The member role could not be updated.");
        }
    }

    public async Task<WorkspaceMemberOperationResult> DeactivateWorkspaceMemberAsync(
        Guid workspaceId,
        Guid membershipId,
        string actorUserId)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, actorUserId);

        if (actorRole is null)
        {
            return WorkspaceMemberOperationResult.Failure("Workspace not found.");
        }

        if (WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count == 0)
        {
            return WorkspaceMemberOperationResult.Failure(
                "You do not have permission to manage workspace members.");
        }

        var membership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.WorkspaceId == workspaceId &&
                member.IsActive);

        if (membership is null)
        {
            return WorkspaceMemberOperationResult.Failure(
                "Active workspace membership not found.");
        }

        var memberError = CheckManageableMember(
            actorRole.Value,
            membership,
            actorUserId,
            "You cannot deactivate your own workspace membership.");

        if (memberError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(memberError);
        }

        membership.IsActive = false;

        try
        {
            await _dbContext.SaveChangesAsync();

            return WorkspaceMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return WorkspaceMemberOperationResult.Failure(
                "The workspace member could not be deactivated.");
        }
    }

    // The user's role in an unarchived workspace, or null when they are not
    // an active member of it.
    private async Task<WorkspaceRole?> GetActiveRoleAsync(
        Guid workspaceId,
        string userId)
    {
        if (workspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
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
            .Select(member => (WorkspaceRole?)member.Role)
            .SingleOrDefaultAsync();
    }

    private static string? CheckAssignableRole(
        WorkspaceRole actorRole,
        WorkspaceRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return "Invalid workspace role.";
        }

        if (role == WorkspaceRole.Owner)
        {
            return "The Owner role cannot be assigned here.";
        }

        if (!WorkspaceMemberRules.CanAssign(actorRole, role))
        {
            return "Only the workspace Owner can make someone an Admin.";
        }

        return null;
    }

    private static string? CheckManageableMember(
        WorkspaceRole actorRole,
        WorkspaceMember membership,
        string actorUserId,
        string selfMessage)
    {
        if (membership.UserId == actorUserId)
        {
            return selfMessage;
        }

        if (membership.Role == WorkspaceRole.Owner)
        {
            return "The workspace Owner cannot be changed here.";
        }

        if (!WorkspaceMemberRules.CanAssign(actorRole, membership.Role))
        {
            return "You do not have permission to manage this member.";
        }

        return null;
    }

    // ---------------------------------------------------------
    // Members
    //
    // Owners can hand out Admin, Member and Viewer. Admins can hand out
    // Member and Viewer. Nobody can create, change or remove an Owner here,
    // and nobody can change their own role or remove themselves. The role
    // hierarchy itself lives in WorkspaceMemberRules.
    // ---------------------------------------------------------

    public async Task<IReadOnlyList<WorkspaceMemberItem>> GetWorkspaceMembersAsync(
        Guid workspaceId,
        string userId)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, userId);

        if (actorRole is null)
        {
            return Array.Empty<WorkspaceMemberItem>();
        }

        var canManage = WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count > 0;

        var query = _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId);

        if (!canManage)
        {
            query = query.Where(member => member.IsActive);
        }

        var members = await query
            .OrderByDescending(member => member.IsActive)
            .ThenBy(member => member.Role)
            .ThenBy(member => member.User.UserName)
            .Select(member => new WorkspaceMemberItem
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
                WorkspaceMemberRules.CanAssign(actorRole.Value, member.Role);
        }

        return members;
    }

    public async Task<bool> CanManageWorkspaceMembersAsync(
        Guid workspaceId,
        string userId)
    {
        var role = await GetActiveRoleAsync(workspaceId, userId);

        return role is not null &&
            WorkspaceMemberRules.AssignableRoles(role.Value).Count > 0;
    }

    public async Task<WorkspaceMemberOperationResult> AddWorkspaceMemberAsync(
        Guid workspaceId,
        string actorUserId,
        string email,
        WorkspaceRole role)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, actorUserId);

        if (actorRole is null)
        {
            return WorkspaceMemberOperationResult.Failure("Workspace not found.");
        }

        if (WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count == 0)
        {
            return WorkspaceMemberOperationResult.Failure(
                "You do not have permission to manage workspace members.");
        }

        var roleError = CheckAssignableRole(actorRole.Value, role);

        if (roleError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(roleError);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return WorkspaceMemberOperationResult.Failure(
                "Please enter a valid email address.");
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail);

        if (targetUser is null)
        {
            return WorkspaceMemberOperationResult.Failure(
                "No registered account was found with this email.");
        }

        var existingMembership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == targetUser.Id);

        if (existingMembership is not null)
        {
            if (existingMembership.Role == WorkspaceRole.Owner)
            {
                return WorkspaceMemberOperationResult.Failure(
                    "The workspace Owner cannot be changed here.");
            }

            if (existingMembership.IsActive)
            {
                return WorkspaceMemberOperationResult.Failure(
                    "This user is already a workspace member.");
            }

            existingMembership.Role = role;
            existingMembership.IsActive = true;
            existingMembership.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = targetUser.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync();

            return WorkspaceMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return WorkspaceMemberOperationResult.Failure(
                "The workspace member could not be added.");
        }
    }

    public async Task<WorkspaceMemberOperationResult> UpdateWorkspaceMemberRoleAsync(
        Guid workspaceId,
        Guid membershipId,
        string actorUserId,
        WorkspaceRole role)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, actorUserId);

        if (actorRole is null)
        {
            return WorkspaceMemberOperationResult.Failure("Workspace not found.");
        }

        if (WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count == 0)
        {
            return WorkspaceMemberOperationResult.Failure(
                "You do not have permission to manage workspace members.");
        }

        var roleError = CheckAssignableRole(actorRole.Value, role);

        if (roleError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(roleError);
        }

        var membership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.WorkspaceId == workspaceId &&
                member.IsActive);

        if (membership is null)
        {
            return WorkspaceMemberOperationResult.Failure(
                "Active workspace membership not found.");
        }

        var memberError = CheckManageableMember(
            actorRole.Value,
            membership,
            actorUserId,
            "You cannot change your own workspace role.");

        if (memberError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(memberError);
        }

        membership.Role = role;

        try
        {
            await _dbContext.SaveChangesAsync();

            return WorkspaceMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return WorkspaceMemberOperationResult.Failure(
                "The member role could not be updated.");
        }
    }

    public async Task<WorkspaceMemberOperationResult> DeactivateWorkspaceMemberAsync(
        Guid workspaceId,
        Guid membershipId,
        string actorUserId)
    {
        var actorRole = await GetActiveRoleAsync(workspaceId, actorUserId);

        if (actorRole is null)
        {
            return WorkspaceMemberOperationResult.Failure("Workspace not found.");
        }

        if (WorkspaceMemberRules.AssignableRoles(actorRole.Value).Count == 0)
        {
            return WorkspaceMemberOperationResult.Failure(
                "You do not have permission to manage workspace members.");
        }

        var membership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(member =>
                member.Id == membershipId &&
                member.WorkspaceId == workspaceId &&
                member.IsActive);

        if (membership is null)
        {
            return WorkspaceMemberOperationResult.Failure(
                "Active workspace membership not found.");
        }

        var memberError = CheckManageableMember(
            actorRole.Value,
            membership,
            actorUserId,
            "You cannot deactivate your own workspace membership.");

        if (memberError is not null)
        {
            return WorkspaceMemberOperationResult.Failure(memberError);
        }

        membership.IsActive = false;

        try
        {
            await _dbContext.SaveChangesAsync();

            return WorkspaceMemberOperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return WorkspaceMemberOperationResult.Failure(
                "The workspace member could not be deactivated.");
        }
    }

    // The user's role in an unarchived workspace, or null when they are not
    // an active member of it.
    private async Task<WorkspaceRole?> GetActiveRoleAsync(
        Guid workspaceId,
        string userId)
    {
        if (workspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(userId))
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
            .Select(member => (WorkspaceRole?)member.Role)
            .SingleOrDefaultAsync();
    }

    private static string? CheckAssignableRole(
        WorkspaceRole actorRole,
        WorkspaceRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return "Invalid workspace role.";
        }

        if (role == WorkspaceRole.Owner)
        {
            return "The Owner role cannot be assigned here.";
        }

        if (!WorkspaceMemberRules.CanAssign(actorRole, role))
        {
            return "Only the workspace Owner can make someone an Admin.";
        }

        return null;
    }

    private static string? CheckManageableMember(
        WorkspaceRole actorRole,
        WorkspaceMember membership,
        string actorUserId,
        string selfMessage)
    {
        if (membership.UserId == actorUserId)
        {
            return selfMessage;
        }

        if (membership.Role == WorkspaceRole.Owner)
        {
            return "The workspace Owner cannot be changed here.";
        }

        if (!WorkspaceMemberRules.CanAssign(actorRole, membership.Role))
        {
            return "You do not have permission to manage this member.";
        }

        return null;
    }
}
