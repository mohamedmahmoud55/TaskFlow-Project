using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Workspaces;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Controllers;

[Authorize]
public sealed class WorkspaceController : Controller
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var workspaces = await _workspaceService.GetUserWorkspacesAsync(userId);
        return View(new WorkspaceListViewModel { Workspaces = workspaces });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateWorkspaceViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateWorkspaceViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _workspaceService.CreateWorkspaceAsync(
            userId,
            model.Name,
            model.Description);

        if (!result.Succeeded || result.WorkspaceId is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The workspace could not be created.");
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = result.WorkspaceId.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(id, userId);
        if (workspace is null)
        {
            return NotFound();
        }

        return View(new WorkspaceDetailsViewModel
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            CurrentUserRole = workspace.Role
        });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
