
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Projects;
using TaskFlow.Services.Teams;
using TaskFlow.Services.Teams.Models;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Controllers;

[Authorize]
public sealed class ProjectController : Controller
{
    private readonly IProjectService _projectService;
    private readonly ITeamService _teamService;

    public ProjectController(
        IProjectService projectService,
        ITeamService teamService)
    {
        _projectService = projectService;
        _teamService = teamService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid teamId, Guid? workspaceId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        var team = await _teamService.GetTeamAsync(teamId, workspaceId, userId);

        if (team is null)
            return NotFound();

        var projects = await _projectService.GetProjectsAsync(
            teamId,
            team.WorkspaceId,
            userId);

        return View(new ProjectListViewModel
        {
            TeamId = team.Id,
            WorkspaceId = team.WorkspaceId,
            WorkspaceName = team.WorkspaceName,
            TeamName = team.Name,
            Projects = projects
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid teamId, Guid? workspaceId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        var team = await _teamService.GetTeamAsync(teamId, workspaceId, userId);

        if (team is null)
            return NotFound();

        SetCreateViewData(team);

        return View(new CreateProjectViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Guid teamId,
        CreateProjectViewModel model,
        Guid? workspaceId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        var team = await _teamService.GetTeamAsync(teamId, workspaceId, userId);

        if (team is null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            SetCreateViewData(team);
            return View(model);
        }

        var result = await _projectService.CreateProjectAsync(
            team.Id,
            team.WorkspaceId,
            userId,
            model.Name,
            model.Description);

        if (!result.Succeeded || result.ProjectId is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "The project could not be created.");

            SetCreateViewData(team);
            return View(model);
        }

        return RedirectToAction(
            nameof(Details),
            new { id = result.ProjectId.Value, teamId = team.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, Guid? teamId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        var project = await _projectService.GetProjectAsync(id, teamId, userId);

        if (project is null)
            return NotFound();

        var members = await _projectService.GetProjectMembersAsync(
            project.Id,
            userId);

        var canManageMembers =
            await _projectService.CanManageProjectMembersAsync(
                project.Id,
                userId);

        return View(new ProjectDetailsViewModel
        {
            Id = project.Id,
            TeamId = project.TeamId,
            WorkspaceId = project.WorkspaceId,
            WorkspaceName = project.WorkspaceName,
            TeamName = project.TeamName,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            CurrentUserRole = project.CurrentUserRole,
            CanManageMembers = canManageMembers,
            Members = members
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        var project = await _projectService.GetProjectAsync(id, null, userId);

        if (project is null)
            return NotFound();

        if (!await _projectService.CanManageProjectAsync(id, userId))
            return Forbid();

        return View(new EditProjectViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProjectViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
            return View(model);

        var result = await _projectService.UpdateProjectAsync(
            model.Id,
            userId,
            model.Name,
            model.Description);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "The project could not be updated.");

            return View(model);
        }

        TempData["Success"] = "Project updated successfully.";

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(UpdateProjectStatusViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please choose a valid project status.";

            return RedirectToAction(nameof(Details), new { id = model.ProjectId });
        }

        var result = await _projectService.UpdateProjectStatusAsync(
            model.ProjectId,
            userId,
            model.Status);

        if (result.Succeeded)
        {
            TempData["Success"] = "Project status updated successfully.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Unable to update the project status.";
        }

        return RedirectToAction(nameof(Details), new { id = model.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(ArchiveProjectViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid project information.";

            return RedirectToAction(nameof(MyProjects));
        }

        var result = await _projectService.ArchiveProjectAsync(
            model.ProjectId,
            userId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to archive the project.";

            return RedirectToAction(nameof(Details), new { id = model.ProjectId });
        }

        TempData["Success"] =
            "Project archived. A manager can restore it from My Projects, under Archived.";

        return RedirectToAction(nameof(MyProjects));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(RestoreProjectViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid project information.";

            return RedirectToAction(nameof(MyProjects), new { archived = true });
        }

        var result = await _projectService.RestoreProjectAsync(
            model.ProjectId,
            userId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to restore the project.";

            return RedirectToAction(nameof(MyProjects), new { archived = true });
        }

        TempData["Success"] = "Project restored successfully.";

        return RedirectToAction(nameof(Details), new { id = model.ProjectId });
    }

    // Add a member to the project.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(AddProjectMemberViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please enter a valid email address and role.";

            return RedirectToAction(
                nameof(Details),
                new { id = model.ProjectId });
        }

        var result = await _projectService.AddProjectMemberAsync(
            model.ProjectId,
            userId,
            model.Email,
            model.Role);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to add the project member.";
        }
        else
        {
            TempData["Success"] = "Project member added successfully.";
        }

        return RedirectToAction(
            nameof(Details),
            new { id = model.ProjectId });
    }

    // Change an existing project member's role.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMemberRole(
        UpdateProjectMemberRoleViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid member role information.";

            return RedirectToAction(
                nameof(Details),
                new { id = model.ProjectId });
        }

        var result = await _projectService.UpdateProjectMemberRoleAsync(
            model.ProjectId,
            model.MembershipId,
            userId,
            model.Role);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to update the member role.";
        }
        else
        {
            TempData["Success"] = "Member role updated successfully.";
        }

        return RedirectToAction(
            nameof(Details),
            new { id = model.ProjectId });
    }

    // Deactivate a project member.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateMember(
        DeactivateProjectMemberViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid member information.";

            return RedirectToAction(
                nameof(Details),
                new { id = model.ProjectId });
        }

        var result = await _projectService.DeactivateProjectMemberAsync(
            model.ProjectId,
            model.MembershipId,
            userId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to deactivate the member.";
        }
        else
        {
            TempData["Success"] = "Project member deactivated successfully.";
        }

        return RedirectToAction(
            nameof(Details),
            new { id = model.ProjectId });
    }

    private void SetCreateViewData(TeamDetails team)
    {
        ViewData["WorkspaceId"] = team.WorkspaceId;
        ViewData["TeamId"] = team.Id;
        ViewData["TeamName"] = team.Name;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
