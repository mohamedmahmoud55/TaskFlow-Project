namespace TaskFlow.Services.Teams.Models;

public sealed record TeamCreationResult(
    bool Succeeded,
    Guid? TeamId,
    string? Error)
{
    public static TeamCreationResult Success(Guid teamId)
        => new(true, teamId, null);

    public static TeamCreationResult Failure(string error)
        => new(false, null, error);
}
