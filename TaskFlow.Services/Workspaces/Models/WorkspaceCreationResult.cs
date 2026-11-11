namespace TaskFlow.Services.Workspaces.Models;

public sealed record WorkspaceCreationResult(
    bool Succeeded,
    Guid? WorkspaceId,
    string? Error)
{
    public static WorkspaceCreationResult Success(Guid workspaceId)
        => new(true, workspaceId, null);

    public static WorkspaceCreationResult Failure(string error)
        => new(false, null, error);
}
