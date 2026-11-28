namespace TaskFlow.Services.Boards.Models;

public enum BoardFailureReason
{
    None = 0,
    NotFound = 1,
    Forbidden = 2,
    Invalid = 3,
    Failed = 4
}

public sealed record BoardOperationResult(
    bool Succeeded,
    BoardFailureReason Reason = BoardFailureReason.None,
    string? ErrorMessage = null)
{
    public static BoardOperationResult Success()
        => new(true);

    public static BoardOperationResult NotFound(string errorMessage = "Board not found.")
        => new(false, BoardFailureReason.NotFound, errorMessage);

    public static BoardOperationResult Forbidden(string errorMessage = "You do not have permission to manage this board.")
        => new(false, BoardFailureReason.Forbidden, errorMessage);

    public static BoardOperationResult Invalid(string errorMessage)
        => new(false, BoardFailureReason.Invalid, errorMessage);

    public static BoardOperationResult Failed(string errorMessage)
        => new(false, BoardFailureReason.Failed, errorMessage);
}
