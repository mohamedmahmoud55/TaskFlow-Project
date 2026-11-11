
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Tasks.Assignments.Models;

namespace TaskFlow.Services.Tasks.Assignments;

public class TaskAssignmentService : ITaskAssignmentService
{
    private readonly ApplicationDbContext _context;

    public TaskAssignmentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TaskAssignmentDetails?> GetAssignmentDetailsAsync(
        Guid taskId,
        string userId)
    {
        var task = await _context.Tasks
            .Include(t => t.BoardColumn)
                .ThenInclude(c => c.Board)
                    .ThenInclude(b => b.Project)
                        .ThenInclude(p => p.Team)
                            .ThenInclude(team => team.Workspace)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t =>
                t.Id == taskId &&
                !t.IsArchived &&
                !t.BoardColumn.Board.IsArchived &&
                !t.BoardColumn.Board.Project.IsArchived &&
                !t.BoardColumn.Board.Project.Team.IsArchived &&
                !t.BoardColumn.Board.Project.Team.Workspace.IsArchived);

        if (task == null)
            return null;

        var project = task.BoardColumn.Board.Project;
        var workspace = project.Team.Workspace;

        var canAccess =
            await _context.WorkspaceMembers.AnyAsync(m =>
                m.WorkspaceId == workspace.Id &&
                m.UserId == userId &&
                m.IsActive &&
                (m.Role == WorkspaceRole.Owner ||
                 m.Role == WorkspaceRole.Admin))
            ||
            await _context.ProjectMembers.AnyAsync(m =>
                m.ProjectId == project.Id &&
                m.UserId == userId &&
                m.IsActive);

        if (!canAccess)
            return null;

        var canManage =
            await _context.WorkspaceMembers.AnyAsync(m =>
                m.WorkspaceId == workspace.Id &&
                m.UserId == userId &&
                m.IsActive &&
                (m.Role == WorkspaceRole.Owner ||
                 m.Role == WorkspaceRole.Admin))
            ||
            await _context.ProjectMembers.AnyAsync(m =>
                m.ProjectId == project.Id &&
                m.UserId == userId &&
                m.IsActive &&
                m.Role == ProjectRole.Manager);

        var assignedUserIds = task.Assignees
            .Select(a => a.UserId)
            .ToHashSet();

        var members = await _context.ProjectMembers
            .Where(m =>
                m.ProjectId == project.Id &&
                m.IsActive &&
                m.Role != ProjectRole.Viewer)
            .Select(m => new TaskAssignmentMember
            {
                UserId = m.UserId,
                DisplayName = m.User.UserName ?? m.User.Email ?? m.UserId,
                Email = m.User.Email,
                IsAssigned = assignedUserIds.Contains(m.UserId)
            })
            .OrderBy(m => m.DisplayName)
            .ToListAsync();

        return new TaskAssignmentDetails
        {
            TaskId = task.Id,
            CanManageAssignments = canManage,
            Members = members
        };
    }

    public async Task<bool> UpdateAssigneesAsync(
        Guid taskId,
        string userId,
        IReadOnlyCollection<string> assigneeUserIds)
    {
        var task = await _context.Tasks
            .Include(t => t.BoardColumn)
                .ThenInclude(c => c.Board)
                    .ThenInclude(b => b.Project)
                        .ThenInclude(p => p.Team)
                            .ThenInclude(team => team.Workspace)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t =>
                t.Id == taskId &&
                !t.IsArchived &&
                !t.BoardColumn.Board.IsArchived &&
                !t.BoardColumn.Board.Project.IsArchived &&
                !t.BoardColumn.Board.Project.Team.IsArchived &&
                !t.BoardColumn.Board.Project.Team.Workspace.IsArchived);

        if (task == null)
            return false;

        var project = task.BoardColumn.Board.Project;
        var workspace = project.Team.Workspace;

        var canManage =
            await _context.WorkspaceMembers.AnyAsync(m =>
                m.WorkspaceId == workspace.Id &&
                m.UserId == userId &&
                m.IsActive &&
                (m.Role == WorkspaceRole.Owner ||
                 m.Role == WorkspaceRole.Admin))
            ||
            await _context.ProjectMembers.AnyAsync(m =>
                m.ProjectId == project.Id &&
                m.UserId == userId &&
                m.IsActive &&
                m.Role == ProjectRole.Manager);

        if (!canManage)
            return false;

        var requestedIds = assigneeUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var eligibleIds = await _context.ProjectMembers
            .Where(m =>
                m.ProjectId == project.Id &&
                m.IsActive &&
                m.Role != ProjectRole.Viewer &&
                requestedIds.Contains(m.UserId))
            .Select(m => m.UserId)
            .ToListAsync();

        if (eligibleIds.Count != requestedIds.Count)
            return false;

        var existingIds = task.Assignees
            .Select(a => a.UserId)
            .ToHashSet();

        var idsToRemove = task.Assignees
            .Where(a => !requestedIds.Contains(a.UserId))
            .ToList();

        _context.TaskAssignees.RemoveRange(idsToRemove);

        var newAssignees = requestedIds
            .Where(id => !existingIds.Contains(id))
            .Select(id => new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                UserId = id,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = userId
            })
            .ToList();

        await _context.TaskAssignees.AddRangeAsync(newAssignees);

        await _context.SaveChangesAsync();

        return true;
    }
}
