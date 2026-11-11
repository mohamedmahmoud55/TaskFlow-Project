using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Projects.Models;

public sealed record ProjectListItem(
    Guid Id,
    Guid TeamId,
    Guid WorkspaceId,
    string Name,
    string? Description,
    ProjectStatus Status,
    ProjectRole? CurrentUserRole);
