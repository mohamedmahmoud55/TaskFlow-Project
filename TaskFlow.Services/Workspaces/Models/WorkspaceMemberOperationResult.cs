namespace TaskFlow.Services.Workspaces.Models;

public record WorkspaceMemberOperationResult(
    bool Succeeded,
    string? Error = null)
{
    public static WorkspaceMemberOperationResult Success()
        => new(true);

    public static WorkspaceMemberOperationResult Failure(string error)
        => new(false, error);
}
