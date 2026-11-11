
namespace TaskFlow.Services.Projects.Models;

public record ProjectMemberOperationResult(
    bool Succeeded,
    string? Error = null)
{
    public static ProjectMemberOperationResult Success()
        => new(true);

    public static ProjectMemberOperationResult Failure(string error)
        => new(false, error);
}
