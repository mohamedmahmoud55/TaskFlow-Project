namespace TaskFlow.Services.Workspaces.Models;

public record WorkspaceOperationResult(
    bool Succeeded,
    string? Error = null)
{
    public static WorkspaceOperationResult Success()
        => new(true);

    public static WorkspaceOperationResult Failure(string error)
        => new(false, error);
}
