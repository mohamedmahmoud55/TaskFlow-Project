
namespace TaskFlow.Data.Entities;

public class Board
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid ProjectId { get; set; }

    // Board Information
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsArchived { get; set; } = false;

    public bool IsDefault { get; set; } = false;

    // Navigation Properties
    public Project Project { get; set; } = null!;

    public ICollection<BoardColumn> Columns { get; set; }
        = new List<BoardColumn>();
}
