namespace TaskFlow.Services.Tasks.Models;

public enum TaskMoveFailureReason
{
    None = 0,
    NotFound = 1,
    Forbidden = 2,
    Invalid = 3,
    WipLimit = 4,
    Conflict = 5
}

public sealed record TaskMoveResult(
    bool Succeeded,
    TaskMoveFailureReason Reason = TaskMoveFailureReason.None,
    string? ErrorMessage = null,
    bool IsCompleted = false)
{
    public static TaskMoveResult Success(bool isCompleted)
        => new(true, IsCompleted: isCompleted);

    public static TaskMoveResult NotFound(string errorMessage = "The task could not be found.")
        => new(false, TaskMoveFailureReason.NotFound, errorMessage);

    public static TaskMoveResult Forbidden(string errorMessage = "You do not have permission to move tasks in this project.")
        => new(false, TaskMoveFailureReason.Forbidden, errorMessage);

    public static TaskMoveResult Invalid(string errorMessage)
        => new(false, TaskMoveFailureReason.Invalid, errorMessage);

    public static TaskMoveResult WipLimit(string errorMessage)
        => new(false, TaskMoveFailureReason.WipLimit, errorMessage);

    public static TaskMoveResult Conflict(string errorMessage = "The board changed, please try again.")
        => new(false, TaskMoveFailureReason.Conflict, errorMessage);
}
