using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Projects;
using TaskFlow.Services.Tasks;
using TaskFlow.Services.Teams;
using TaskFlow.Services.Workspaces;

namespace TaskFlow.Web.ViewComponents;

// Url is null for the current page and for crumbs the user cannot open.
public sealed record BreadcrumbItem(string Text, string? Url, bool IsCurrent = false);

/// <summary>
/// One breadcrumb trail for every page:
/// My Workspaces / Workspace / Team / Project / Board / current page.
///
/// A page passes the most specific thing it knows and the trail above it is
/// looked up (with the user's access rules applied):
///   boardColumnId  task pages, which only know the column
///   projectId      project and board pages
///   teamId         pages inside a team
///   workspaceId    pages inside a workspace
/// The last crumb is always the current page and is not a link. Workspace
/// and team crumbs are only links when the user can open those pages, so a
/// guest added to a single project never meets a dead link.
/// </summary>
public class BreadcrumbsViewComponent : ViewComponent
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ITeamService _teamService;
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;

    public BreadcrumbsViewComponent(
        IWorkspaceService workspaceService,
        ITeamService teamService,
        IProjectService projectService,
        ITaskService taskService)
    {
        _workspaceService = workspaceService;
        _teamService = teamService;
        _projectService = projectService;
        _taskService = taskService;
    }

    public async Task<IViewComponentResult> InvokeAsync(
        Guid? workspaceId = null,
        Guid? teamId = null,
        Guid? projectId = null,
        Guid? boardColumnId = null,
        Guid? boardId = null,
        string? boardName = null,
        string? current = null)
    {
        var crumbs = new List<BreadcrumbItem>
        {
            new("My Workspaces", Url.Action("Index", "Workspace"))
        };

        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            if (boardColumnId.HasValue)
            {
                var column = await _taskService.GetColumnContextAsync(
                    boardColumnId.Value,
                    userId);

                if (column is not null)
                {
                    projectId = column.ProjectId;
                    boardId = column.BoardId;
                    boardName = column.BoardName;
                }
            }

            if (projectId.HasValue)
            {
                var project = await _projectService.GetProjectAsync(
                    projectId.Value,
                    null,
                    userId);

                if (project is not null)
                {
                    var canOpenWorkspace = await CanOpenWorkspaceAsync(
                        project.WorkspaceId,
                        userId);

                    var canOpenTeam = await _teamService.GetTeamAsync(
                        project.TeamId,
                        project.WorkspaceId,
                        userId) is not null;

                    if (!canOpenWorkspace)
                    {
                        // A guest has no workspace list to go back to, so
                        // the trail starts from their projects instead.
                        crumbs[0] = new("My Projects", Url.Action("MyProjects", "Project"));
                    }

                    crumbs.Add(new(
                        project.WorkspaceName,
                        canOpenWorkspace
                            ? Url.Action("Details", "Workspace", new { id = project.WorkspaceId })
                            : null));

                    crumbs.Add(new(
                        project.TeamName,
                        canOpenTeam
                            ? Url.Action("Details", "Team", new { id = project.TeamId, workspaceId = project.WorkspaceId })
                            : null));

                    crumbs.Add(new(
                        project.Name,
                        Url.Action("Details", "Project", new { id = project.Id })));

                    if (boardId.HasValue && !string.IsNullOrWhiteSpace(boardName))
                    {
                        crumbs.Add(new(
                            boardName,
                            Url.Action("Details", "Board", new { id = boardId.Value, projectId = project.Id })));
                    }
                }
            }
            else if (teamId.HasValue)
            {
                var team = await _teamService.GetTeamAsync(
                    teamId.Value,
                    null,
                    userId);

                if (team is not null)
                {
                    crumbs.Add(new(
                        team.WorkspaceName,
                        Url.Action("Details", "Workspace", new { id = team.WorkspaceId })));

                    crumbs.Add(new(
                        team.Name,
                        Url.Action("Details", "Team", new { id = team.Id, workspaceId = team.WorkspaceId })));
                }
            }
            else if (workspaceId.HasValue)
            {
                var workspace = await _workspaceService.GetWorkspaceAsync(
                    workspaceId.Value,
                    userId);

                if (workspace is not null)
                {
                    crumbs.Add(new(
                        workspace.Name,
                        Url.Action("Details", "Workspace", new { id = workspace.Id })));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            crumbs.Add(new(current, null));
        }

        // The last crumb is the page being viewed, so it is never a link.
        crumbs[^1] = crumbs[^1] with { Url = null, IsCurrent = true };

        return View(crumbs);
    }

    private async Task<bool> CanOpenWorkspaceAsync(Guid workspaceId, string userId)
    {
        return await _workspaceService.GetWorkspaceAsync(workspaceId, userId) is not null;
    }
}
