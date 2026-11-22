namespace TaskFlow.Services.Teams.Models;

public record TeamOperationResult(
    bool Succeeded,
    string? Error = null)
{
    public static TeamOperationResult Success()
        => new(true);

    public static TeamOperationResult Failure(string error)
        => new(false, error);
}
