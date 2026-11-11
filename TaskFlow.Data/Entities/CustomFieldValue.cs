namespace TaskFlow.Data.Entities;

public class CustomFieldValue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomFieldDefinitionId { get; set; }
    public Guid TaskId { get; set; }
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public DateTime? DateValue { get; set; }
    public bool? BooleanValue { get; set; }
    public string? SelectedValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public CustomFieldDefinition CustomFieldDefinition { get; set; } = null!;
    public Task Task { get; set; } = null!;
}
