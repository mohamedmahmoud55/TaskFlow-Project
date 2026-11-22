namespace TaskFlow.Services.Projects.Models;

public record ProjectOperationResult(
    bool Succeeded,
    string? Error = null)
{
    public static ProjectOperationResult Success()
        => new(true);

    public static ProjectOperationResult Failure(string error)
        => new(false, error);
}
