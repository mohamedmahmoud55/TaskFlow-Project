
namespace TaskFlow.Data.Entities;

public class BoardColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BoardId { get; set; }

    // Column Information
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Position { get; set; }

    public int? WipLimit { get; set; }

    public bool IsDefault { get; set; } = false;

    public bool IsCompleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public Board Board { get; set; } = null!;
    public ICollection<Task> Tasks { get; set; }
        = new List<Task>();
}
