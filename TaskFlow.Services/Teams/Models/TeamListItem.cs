using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Teams.Models;

public sealed record TeamListItem(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    TeamRole? CurrentUserRole);
