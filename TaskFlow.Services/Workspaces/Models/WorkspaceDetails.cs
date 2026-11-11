using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Workspaces.Models;

public sealed record WorkspaceDetails(
    Guid Id,
    string Name,
    string? Description,
    WorkspaceRole Role);
