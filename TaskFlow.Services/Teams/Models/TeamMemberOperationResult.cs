namespace TaskFlow.Services.Teams.Models;

public record TeamMemberOperationResult(
    bool Succeeded,
    string? Error = null)
{
    public static TeamMemberOperationResult Success()
        => new(true);

    public static TeamMemberOperationResult Failure(string error)
        => new(false, error);
}
