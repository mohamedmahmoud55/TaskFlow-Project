using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class CustomFieldDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; }
    public string? OptionsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Project? Project { get; set; }
    public ICollection<CustomFieldValue> Values { get; set; } = new List<CustomFieldValue>();
}
