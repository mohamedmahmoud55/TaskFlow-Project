
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Boards;
using TaskFlow.Services.Projects;
using TaskFlow.Web.Models.Boards;
using TaskFlow.Services.Tasks;
namespace TaskFlow.Web.Controllers;

[Authorize]
public sealed class BoardController : Controller
{
    private readonly IBoardService _boardService;
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;

    public BoardController(
     IBoardService boardService,
     IProjectService projectService,
     ITaskService taskService)
    {
        _boardService = boardService;
        _projectService = projectService;
        _taskService = taskService;
    }

    // ---------------------------------------------------------
    // List boards
    // ---------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid projectId,
        Guid? teamId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var project = await _projectService.GetProjectAsync(
            projectId,
            teamId,
            userId);

        if (project is null)
        {
            return NotFound();
        }

        var boards = await _boardService.GetBoardsAsync(
            project.Id,
            project.TeamId,
            userId);

        return View(new BoardListViewModel
        {
            ProjectId = project.Id,
            TeamId = project.TeamId,
            ProjectName = project.Name,
            Boards = boards
        });
    }

    // ---------------------------------------------------------
    // Create board - GET
    // ---------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Create(
        Guid projectId,
        Guid? teamId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var project = await _projectService.GetProjectAsync(
            projectId,
            teamId,
            userId);

        if (project is null)
        {
            return NotFound();
        }

        var canManage = await _boardService
            .CanManageProjectBoardsAsync(project.Id, userId);

        if (!canManage)
        {
            return Forbid();
        }

        return View(new CreateBoardViewModel
        {
            ProjectId = project.Id,
            TeamId = project.TeamId
        });
    }

    // ---------------------------------------------------------
    // Create board - POST
    // ---------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Guid projectId,
        CreateBoardViewModel model,
        Guid? teamId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var project = await _projectService.GetProjectAsync(
            projectId,
            teamId,
            userId);

        if (project is null)
        {
            return NotFound();
        }

        var canManage = await _boardService
            .CanManageProjectBoardsAsync(project.Id, userId);

        if (!canManage)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.ProjectId = project.Id;
            model.TeamId = project.TeamId;
            return View(model);
        }

        var result = await _boardService.CreateBoardAsync(
            project.Id,
            project.TeamId,
            userId,
            model.Name,
            model.Description,
            model.IsDefault);

        if (!result.Succeeded || result.BoardId is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "The board could not be created.");

            model.ProjectId = project.Id;
            model.TeamId = project.TeamId;

            return View(model);
        }

        return RedirectToAction(
            nameof(Details),
            new
            {
                id = result.BoardId.Value,
                projectId = project.Id
            });
    }

    // ---------------------------------------------------------
    // Board details
    // ---------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        Guid? projectId = null)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var board = await _boardService.GetBoardAsync(
            id,
            projectId,
            userId);

        if (board is null)
        {
            return NotFound();
        }

        var project = await _projectService.GetProjectAsync(
            board.ProjectId,
            null,
            userId);

        if (project is null)
        {
            return NotFound();
        }

        var canManage = await _boardService
            .CanManageProjectBoardsAsync(project.Id, userId);
        var tasksByColumn = new Dictionary<Guid, IReadOnlyList<TaskFlow.Services.Tasks.Models.TaskListItem>>();

        foreach (var column in board.Columns)
        {
            var tasks = await _taskService.GetTasksAsync(
                column.Id,
                userId);

            tasksByColumn[column.Id] = tasks;
        }
        return View(new BoardDetailsViewModel
        {
            Id = board.Id,
            ProjectId = project.Id,
            TeamId = project.TeamId,
            ProjectName = project.Name,
            Name = board.Name,
            Description = board.Description,
            IsDefault = board.IsDefault,
            CanManage = canManage,
            Columns = board.Columns,
            TasksByColumn = tasksByColumn
        });
    }

    // ---------------------------------------------------------
    // Create column
    // ---------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateColumn(
        Guid boardId,
        CreateBoardColumnViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var board = await _boardService.GetBoardAsync(
            boardId,
            null,
            userId);

        if (board is null)
        {
            return NotFound();
        }

        if (!await _boardService.CanManageProjectBoardsAsync(
                board.ProjectId, userId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                "Please provide valid column information.";

            return RedirectToAction(
                nameof(Details),
                new { id = boardId });
        }

        var result = await _boardService.CreateColumnAsync(
            boardId,
            userId,
            model.Name,
            model.Description,
            model.WipLimit,
            model.IsCompleted);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Column created successfully."
                : result.ErrorMessage ?? "The column could not be created.";

        return RedirectToAction(
            nameof(Details),
            new { id = boardId });
    }

    // ---------------------------------------------------------
    // Update column
    // ---------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateColumn(
        Guid boardId,
        UpdateBoardColumnViewModel model)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var board = await _boardService.GetBoardAsync(
            boardId,
            null,
            userId);

        if (board is null)
        {
            return NotFound();
        }

        if (!board.Columns.Any(column => column.Id == model.ColumnId))
        {
            return NotFound();
        }

        if (!await _boardService.CanManageProjectBoardsAsync(
                board.ProjectId, userId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                "Please provide valid column information.";

            return RedirectToAction(
                nameof(Details),
                new { id = boardId });
        }

        var succeeded = await _boardService.UpdateColumnAsync(
            model.ColumnId,
            userId,
            model.Name,
            model.Description,
            model.WipLimit,
            model.IsCompleted);

        TempData[succeeded ? "SuccessMessage" : "ErrorMessage"] =
            succeeded
                ? "Column updated successfully."
                : "The column could not be updated.";

        return RedirectToAction(
            nameof(Details),
            new { id = boardId });
    }

    // ---------------------------------------------------------
    // Reorder columns
    // ---------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderColumns(
        Guid boardId,
        [FromForm] List<Guid> orderedColumnIds)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var board = await _boardService.GetBoardAsync(
            boardId,
            null,
            userId);

        if (board is null)
        {
            return NotFound();
        }

        if (!await _boardService.CanManageProjectBoardsAsync(
                board.ProjectId, userId))
        {
            return Forbid();
        }

        var succeeded = await _boardService.ReorderColumnsAsync(
            boardId,
            userId,
            orderedColumnIds);

        TempData[succeeded ? "SuccessMessage" : "ErrorMessage"] =
            succeeded
                ? "Columns reordered successfully."
                : "The column order could not be updated.";

        return RedirectToAction(
            nameof(Details),
            new { id = boardId });
    }

    // ---------------------------------------------------------
    // Current user
    // ---------------------------------------------------------

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
