using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Workspaces.Models;

public sealed record WorkspaceListItem(
    Guid Id,
    string Name,
    string? Description,
    WorkspaceRole Role);
