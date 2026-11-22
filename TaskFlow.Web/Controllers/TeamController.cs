using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Teams;
using TaskFlow.Services.Teams.Models;
using TaskFlow.Services.Workspaces;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Controllers;

[Authorize]
public sealed class TeamController : Controller
{
    private readonly ITeamService _teamService;
    private readonly IWorkspaceService _workspaceService;

    public TeamController(ITeamService teamService, IWorkspaceService workspaceService)
    {
        _teamService = teamService;
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid workspaceId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId, userId);
        if (workspace is null)
        {
            return NotFound();
        }

        var teams = await _teamService.GetTeamsAsync(workspaceId, userId);

        var archivedTeams = WorkspaceRules.CanEdit(workspace.Role)
            ? await _teamService.GetArchivedTeamsAsync(workspaceId, userId)
            : Array.Empty<TeamListItem>();

        return View(new TeamListViewModel
        {
            WorkspaceId = workspaceId,
            WorkspaceName = workspace.Name,
            Teams = teams,
            ArchivedTeams = archivedTeams
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid workspaceId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId, userId);
        if (workspace is null)
        {
            return NotFound();
        }

        ViewData["WorkspaceId"] = workspaceId;
        return View(new CreateTeamViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid workspaceId, CreateTeamViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["WorkspaceId"] = workspaceId;
            return View(model);
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _teamService.CreateTeamAsync(
            workspaceId,
            userId,
            model.Name,
            model.Description);

        if (!result.Succeeded || result.TeamId is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The team could not be created.");
            ViewData["WorkspaceId"] = workspaceId;
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = result.TeamId.Value, workspaceId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, Guid? workspaceId = null)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var team = await _teamService.GetTeamAsync(id, workspaceId, userId);
        if (team is null)
        {
            return NotFound();
        }

        var members = await _teamService.GetTeamMembersAsync(id, userId);

        var management = await _teamService.GetTeamMemberManagementAsync(id, userId);

        var lifecycle = await _teamService.GetTeamLifecycleAccessAsync(id, userId);

        return View(new TeamDetailsViewModel
        {
            Id = team.Id,
            WorkspaceId = team.WorkspaceId,
            WorkspaceName = team.WorkspaceName,
            Name = team.Name,
            Description = team.Description,
            CurrentUserRole = team.CurrentUserRole,
            CanManageMembers = management.CanManage,
            AssignableRoles = management.AssignableRoles,
            Candidates = management.Candidates,
            Members = members,
            CanEditTeam = lifecycle.CanEdit,
            CanArchiveTeam = lifecycle.CanArchive
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, Guid? workspaceId = null)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var team = await _teamService.GetTeamAsync(id, workspaceId, userId);
        if (team is null)
        {
            return NotFound();
        }

        var lifecycle = await _teamService.GetTeamLifecycleAccessAsync(id, userId);
        if (!lifecycle.CanEdit)
        {
            return Forbid();
        }

        return View(new EditTeamViewModel
        {
            Id = team.Id,
            WorkspaceId = team.WorkspaceId,
            Name = team.Name,
            Description = team.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditTeamViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _teamService.UpdateTeamAsync(
            model.Id,
            userId,
            model.Name,
            model.Description);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "The team could not be updated.");

            return View(model);
        }

        TempData["Success"] = "Team updated successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id = model.Id, workspaceId = model.WorkspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(ArchiveTeamViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid team information.";

            return RedirectToAction(nameof(Index), new { workspaceId = model.WorkspaceId });
        }

        var result = await _teamService.ArchiveTeamAsync(model.TeamId, userId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to archive the team.";

            return RedirectToAction(
                nameof(Details),
                new { id = model.TeamId, workspaceId = model.WorkspaceId });
        }

        TempData["Success"] =
            "Team archived. A workspace Owner or Admin can restore it from the Teams page, under Archived teams.";

        return RedirectToAction(nameof(Index), new { workspaceId = model.WorkspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(RestoreTeamViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid team information.";

            return RedirectToAction(nameof(Index), new { workspaceId = model.WorkspaceId });
        }

        var result = await _teamService.RestoreTeamAsync(model.TeamId, userId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to restore the team.";

            return RedirectToAction(nameof(Index), new { workspaceId = model.WorkspaceId });
        }

        TempData["Success"] = "Team restored successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id = model.TeamId, workspaceId = model.WorkspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(AddTeamMemberViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please choose a person and a role.";

            return RedirectToAction(nameof(Details), new { id = model.TeamId });
        }

        var result = await _teamService.AddTeamMemberAsync(
            model.TeamId,
            userId,
            model.UserId,
            model.Role);

        if (result.Succeeded)
        {
            TempData["Success"] = "Team member added successfully.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Unable to add the team member.";
        }

        return RedirectToAction(nameof(Details), new { id = model.TeamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMemberRole(UpdateTeamMemberRoleViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid member role information.";

            return RedirectToAction(nameof(Details), new { id = model.TeamId });
        }

        var result = await _teamService.UpdateTeamMemberRoleAsync(
            model.TeamId,
            model.MembershipId,
            userId,
            model.Role);

        if (result.Succeeded)
        {
            TempData["Success"] = "Member role updated successfully.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Unable to update the member role.";
        }

        return RedirectToAction(nameof(Details), new { id = model.TeamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateMember(DeactivateTeamMemberViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid member information.";

            return RedirectToAction(nameof(Details), new { id = model.TeamId });
        }

        var result = await _teamService.DeactivateTeamMemberAsync(
            model.TeamId,
            model.MembershipId,
            userId);

        if (result.Succeeded)
        {
            TempData["Success"] = "Team member deactivated successfully.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Unable to deactivate the member.";
        }

        return RedirectToAction(nameof(Details), new { id = model.TeamId });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
