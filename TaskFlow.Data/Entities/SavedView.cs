using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class SavedView
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid? WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SavedViewType ViewType { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public bool IsDefault { get; set; }
    public bool IsShared { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Workspace? Workspace { get; set; }
    public Project? Project { get; set; }
}
