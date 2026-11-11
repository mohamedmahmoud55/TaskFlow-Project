namespace TaskFlow.Services.Projects.Models;

public sealed record ProjectCreationResult(
    bool Succeeded,
    Guid? ProjectId,
    string? Error)
{
    public static ProjectCreationResult Success(Guid projectId)
        => new(true, projectId, null);

    public static ProjectCreationResult Failure(string error)
        => new(false, null, error);
}
