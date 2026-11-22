namespace TaskFlow.Services.Teams.Models;

// A workspace member who can be added to a team.
public sealed record TeamMemberCandidate(
    string UserId,
    string DisplayName,
    string Email);
