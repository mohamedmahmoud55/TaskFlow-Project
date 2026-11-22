using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Projects.Models;

// One row of the "My Projects" page. CurrentUserRole is null when the user
// can open the project only as an administrator of its workspace.
public sealed record MyProjectItem(
    Guid Id,
    string Name,
    ProjectStatus Status,
    Guid TeamId,
    string TeamName,
    Guid WorkspaceId,
    string WorkspaceName,
    ProjectRole? CurrentUserRole);
