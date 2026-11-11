using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Projects.Models;

public sealed record ProjectDetails(
    Guid Id,
    Guid TeamId,
    Guid WorkspaceId,
    string WorkspaceName,
    string TeamName,
    string Name,
    string? Description,
    ProjectStatus Status,
    ProjectRole? CurrentUserRole);
