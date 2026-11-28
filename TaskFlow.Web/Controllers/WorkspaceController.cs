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
    public async Task<IActionResult> Index(bool archived = false)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var workspaces = archived
            ? await _workspaceService.GetArchivedWorkspacesAsync(userId)
            : await _workspaceService.GetUserWorkspacesAsync(userId);

        return View(new WorkspaceListViewModel
        {
            ShowingArchived = archived,
            Workspaces = workspaces
        });
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

        var members = await _workspaceService.GetWorkspaceMembersAsync(id, userId);

        var canManageMembers = await _workspaceService.CanManageWorkspaceMembersAsync(id, userId);

        return View(new WorkspaceDetailsViewModel
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            CurrentUserRole = workspace.Role,
            CanManageMembers = canManageMembers,
            AssignableRoles = WorkspaceMemberRules.AssignableRoles(workspace.Role),
            Members = members,
            CanEditWorkspace = WorkspaceRules.CanEdit(workspace.Role),
            CanArchiveWorkspace = WorkspaceRules.CanArchive(workspace.Role)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
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

        if (!WorkspaceRules.CanEdit(workspace.Role))
        {
            return Forbid();
        }

        return View(new EditWorkspaceViewModel
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditWorkspaceViewModel model)
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

        var result = await _workspaceService.UpdateWorkspaceAsync(
            model.Id,
            userId,
            model.Name,
            model.Description);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "The workspace could not be updated.");

            return View(model);
        }

        TempData["Success"] = "Workspace updated successfully.";

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(ArchiveWorkspaceViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid workspace information.";

            return RedirectToAction(nameof(Index));
        }

        var result = await _workspaceService.ArchiveWorkspaceAsync(
            model.WorkspaceId,
            userId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to archive the workspace.";

            return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
        }

        TempData["Success"] =
            "Workspace archived. You can restore it from My Workspaces, under Archived.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(RestoreWorkspaceViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid workspace information.";

            return RedirectToAction(nameof(Index), new { archived = true });
        }

        var result = await _workspaceService.RestoreWorkspaceAsync(
            model.WorkspaceId,
            userId);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error ?? "Unable to restore the workspace.";

            return RedirectToAction(nameof(Index), new { archived = true });
        }

        TempData["Success"] = "Workspace restored successfully.";

        return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(AddWorkspaceMemberViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please enter a valid email address and role.";

            return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
        }

        var result = await _workspaceService.AddWorkspaceMemberAsync(
            model.WorkspaceId,
            userId,
            model.Email,
            model.Role);

        if (result.Succeeded)
        {
            TempData["Success"] = "Workspace member added successfully.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Unable to add the workspace member.";
        }

        return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMemberRole(UpdateWorkspaceMemberRoleViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid member role information.";

            return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
        }

        var result = await _workspaceService.UpdateWorkspaceMemberRoleAsync(
            model.WorkspaceId,
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

        return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateMember(DeactivateWorkspaceMemberViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid member information.";

            return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
        }

        var result = await _workspaceService.DeactivateWorkspaceMemberAsync(
            model.WorkspaceId,
            model.MembershipId,
            userId);

        if (result.Succeeded)
        {
            TempData["Success"] = "Workspace member deactivated successfully.";
        }
        else
        {
            TempData["Error"] = result.Error ?? "Unable to deactivate the member.";
        }

        return RedirectToAction(nameof(Details), new { id = model.WorkspaceId });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
