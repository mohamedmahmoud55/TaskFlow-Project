using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Teams.Models;

public sealed record TeamDetails(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string Name,
    string? Description,
    TeamRole? CurrentUserRole);
