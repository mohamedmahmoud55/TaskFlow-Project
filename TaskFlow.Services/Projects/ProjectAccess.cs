using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Projects;

/// <summary>
/// The combined read / write / manage permissions for a single user on a single project.
/// Built once per request and reused to avoid three separate DB round-trips.
/// </summary>
internal readonly record struct ProjectPermissions(bool CanRead, bool CanWrite, bool CanManage);


/// <summary>
/// The single definition of who may read, contribute to and manage a project.
/// Every rule also requires the project, its team and its workspace to be
/// unarchived.
///
///   ReadableBy    workspace Owner/Admin, or any active project member
///   WritableBy    workspace Owner/Admin, or an active project Manager or Member
///   ManageableBy  workspace Owner/Admin, or an active project Manager
///
/// Each level includes the one below it, so a Viewer can read but not write.
/// </summary>
internal static class ProjectAccess
{
    public static IQueryable<Project> ReadableBy(
        this IQueryable<Project> projects,
        string userId)
    {
        return Active(projects).Where(project =>
            project.Team.Workspace.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive &&
                (member.Role == WorkspaceRole.Owner ||
                 member.Role == WorkspaceRole.Admin))
            ||
            project.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive));
    }

    public static IQueryable<Project> WritableBy(
        this IQueryable<Project> projects,
        string userId)
    {
        return Active(projects).Where(project =>
            project.Team.Workspace.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive &&
                (member.Role == WorkspaceRole.Owner ||
                 member.Role == WorkspaceRole.Admin))
            ||
            project.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive &&
                (member.Role == ProjectRole.Manager ||
                 member.Role == ProjectRole.Member)));
    }

    public static IQueryable<Project> ManageableBy(
        this IQueryable<Project> projects,
        string userId)
    {
        return Active(projects).Where(project =>
            project.Team.Workspace.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive &&
                (member.Role == WorkspaceRole.Owner ||
                 member.Role == WorkspaceRole.Admin))
            ||
            project.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive &&
                member.Role == ProjectRole.Manager));
    }

    // Archived projects (in an unarchived team and workspace) that the user
    // could manage, so they can be restored. Every rule above requires a
    // project to be unarchived, so archived projects are invisible to them.
    public static IQueryable<Project> RestorableBy(
        this IQueryable<Project> projects,
        string userId)
    {
        return projects.Where(project =>
            project.IsArchived &&
            !project.Team.IsArchived &&
            !project.Team.Workspace.IsArchived &&
            (project.Team.Workspace.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive &&
                (member.Role == WorkspaceRole.Owner ||
                 member.Role == WorkspaceRole.Admin))
             ||
             project.Members.Any(member =>
                member.UserId == userId &&
                member.IsActive &&
                member.Role == ProjectRole.Manager)));
    }

    /// <summary>
    /// Returns the combined permissions of <paramref name="userId"/> on
    /// <paramref name="projectId"/> in a single async DB trip.
    /// </summary>
    public static async System.Threading.Tasks.Task<ProjectPermissions> GetPermissionsAsync(
        ApplicationDbContext dbContext,
        Guid projectId,
        string userId)
    {
        var row = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                IsWorkspaceAdmin = project.Team.Workspace.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    (member.Role == WorkspaceRole.Owner ||
                     member.Role == WorkspaceRole.Admin)),
                MemberRole = project.Members
                    .Where(member => member.UserId == userId && member.IsActive)
                    .Select(member => (ProjectRole?)member.Role)
                    .FirstOrDefault(),
                ProjectIsArchived      = project.IsArchived,
                TeamIsArchived         = project.Team.IsArchived,
                WorkspaceIsArchived    = project.Team.Workspace.IsArchived
            })
            .SingleOrDefaultAsync();

        if (row is null ||
            row.ProjectIsArchived ||
            row.TeamIsArchived ||
            row.WorkspaceIsArchived)
        {
            return new ProjectPermissions(false, false, false);
        }

        var canRead   = row.IsWorkspaceAdmin || row.MemberRole is not null;
        var canWrite  = row.IsWorkspaceAdmin ||
                        row.MemberRole is ProjectRole.Manager or ProjectRole.Member;
        var canManage = row.IsWorkspaceAdmin ||
                        row.MemberRole is ProjectRole.Manager;

        return new ProjectPermissions(canRead, canWrite, canManage);
    }

    private static IQueryable<Project> Active(IQueryable<Project> projects)
    {
        return projects.Where(project =>
            !project.IsArchived &&
            !project.Team.IsArchived &&
            !project.Team.Workspace.IsArchived);
    }
}
