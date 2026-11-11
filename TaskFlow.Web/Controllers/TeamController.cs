using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Teams;
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
        return View(new TeamListViewModel
        {
            WorkspaceId = workspaceId,
            WorkspaceName = workspace.Name,
            Teams = teams
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

        return View(new TeamDetailsViewModel
        {
            Id = team.Id,
            WorkspaceId = team.WorkspaceId,
            WorkspaceName = team.WorkspaceName,
            Name = team.Name,
            Description = team.Description,
            CurrentUserRole = team.CurrentUserRole
        });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
