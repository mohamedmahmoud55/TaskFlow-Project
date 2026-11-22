
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Tasks;
using TaskFlow.Services.Tasks.Models;
using TaskFlow.Web.Models.Tasks;
using System.Security.Claims;
using TaskFlow.Services.Tasks.Assignments;
using TaskFlow.Web.Models.Task;
using TaskFlow.Services.Tasks.Assignments.Models;

namespace TaskFlow.Web.Controllers;

[Authorize]
public class TaskController : Controller
{
    private readonly ITaskService _taskService;
    private readonly ITaskAssignmentService _taskAssignmentService;

    public TaskController(ITaskService taskService, ITaskAssignmentService taskAssignmentService)
    {
        _taskService = taskService;
        _taskAssignmentService = taskAssignmentService;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    // GET: /Task/Index?boardColumnId=...
    public async Task<IActionResult> Index(Guid boardColumnId)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var tasks = await _taskService.GetTasksAsync(boardColumnId, userId);

        var canCreateTasks = await _taskService.CanCreateTasksAsync(
            boardColumnId,
            userId);

        return View(new TaskListViewModel
        {
            BoardColumnId = boardColumnId,
            CanCreateTasks = canCreateTasks,
            Tasks = tasks
        });
    }

    // GET: /Task/Details/{id}
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var task = await _taskService.GetTaskAsync(id, null, userId);

        if (task == null)
            return NotFound();
        var assignmentDetails =
    await _taskAssignmentService.GetAssignmentDetailsAsync(id, userId);

        if (assignmentDetails == null)
            return NotFound();

        ViewBag.AssignmentDetails = assignmentDetails;

        return View(task);
    }

    // GET: /Task/Create?boardColumnId=...
    [HttpGet]
    public async Task<IActionResult> Create(Guid boardColumnId)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!await _taskService.CanCreateTasksAsync(boardColumnId, userId))
            return Forbid();

        var model = new CreateTaskViewModel
        {
            BoardColumnId = boardColumnId
        };

        return View(model);
    }

    // POST: /Task/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTaskViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var result = await _taskService.CreateTaskAsync(
            model.BoardColumnId,
            userId,
            model.Title,
            model.Description,
            model.Priority,
            model.StartDate,
            model.DueDate,
            model.EstimatedMinutes
        );

        if (!result.Succeeded || !result.TaskId.HasValue)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Unable to create the task."
            );

            return View(model);
        }

        TempData["SuccessMessage"] = "Task created successfully.";

        var createdTask = await _taskService.GetTaskAsync(
            result.TaskId.Value,
            null,
            userId
        );

        if (createdTask == null)
        {
            return RedirectToAction(
                nameof(Index),
                new { boardColumnId = model.BoardColumnId }
            );
        }

        return RedirectToAction(
            "Details",
            "Board",
            new { id = createdTask.BoardId }
        );
    }

    // GET: /Task/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var task = await _taskService.GetTaskAsync(id, null, userId);

        if (task == null)
            return NotFound();

        if (!task.CanEdit)
            return Forbid();

        var model = new EditTaskViewModel
        {
            Id = task.Id,
            BoardColumnId = task.BoardColumnId,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            EstimatedMinutes = task.EstimatedMinutes
        };

        return View(model);
    }

    // POST: /Task/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditTaskViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var result = await _taskService.UpdateTaskAsync(
            model.Id,
            userId,
            model.Title,
            model.Description,
            model.Priority,
            model.StartDate,
            model.DueDate,
            model.EstimatedMinutes
        );

        if (!result.Succeeded)
        {
            if (result.Reason == TaskFailureReason.NotFound)
                return NotFound();

            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "The task could not be updated.");

            return View(model);
        }

        TempData["SuccessMessage"] = "Task updated successfully.";

        var updatedTask = await _taskService.GetTaskAsync(
            model.Id,
            null,
            userId
        );

        if (updatedTask == null)
        {
            return RedirectToAction(
                nameof(Index),
                new { boardColumnId = model.BoardColumnId }
            );
        }

        return RedirectToAction(
            "Details",
            "Board",
            new { id = updatedTask.BoardId }
        );
    }

    // POST: /Task/Archive/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var task = await _taskService.GetTaskAsync(id, null, userId);

        if (task == null)
            return NotFound();

        var succeeded = await _taskService.ArchiveTaskAsync(id, userId);

        if (!succeeded)
        {
            TempData["ErrorMessage"] =
                "The task could not be archived. You may not have permission to change it.";

            return RedirectToAction(
                "Details",
                "Board",
                new { id = task.BoardId }
            );
        }

        TempData["SuccessMessage"] = "Task archived successfully.";

        return RedirectToAction(
            "Details",
            "Board",
            new { id = task.BoardId }
        );
    }


    // POST: /Task/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var task = await _taskService.GetTaskAsync(id, null, userId);

        if (task == null)
            return NotFound();

        var succeeded = await _taskService.DeleteTaskAsync(id, userId);

        if (!succeeded)
        {
            TempData["ErrorMessage"] =
                "The task could not be deleted. You may not have permission to change it.";

            return RedirectToAction(
                "Details",
                "Board",
                new { id = task.BoardId }
            );
        }

        TempData["SuccessMessage"] = "Task deleted successfully.";

        return RedirectToAction(
            "Details",
            "Board",
            new { id = task.BoardId }
        );
    }

    // POST: /Task/UpdateAssignees
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAssignees(
        UpdateTaskAssigneesViewModel model)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid assignment information.";

            return RedirectToAction(
                nameof(Details),
                new { id = model.TaskId }
            );
        }

        var success = await _taskAssignmentService.UpdateAssigneesAsync(
            model.TaskId,
            userId,
            model.SelectedUserIds);

        if (!success)
        {
            TempData["Error"] =
                "Unable to update assignments. You may not have permission, or one or more selected users are not eligible.";
        }
        else
        {
            TempData["Success"] =
                "Task assignments updated successfully.";
        }

        return RedirectToAction(
            nameof(Details),
            new { id = model.TaskId }
        );
    }
}

