namespace TaskFlow.Services.Teams.Models;


// What the current user may do with the team itself.
public sealed record TeamLifecycleAccess(
    bool CanEdit,
    bool CanArchive)
{
    public static TeamLifecycleAccess None { get; } = new(false, false);
}
