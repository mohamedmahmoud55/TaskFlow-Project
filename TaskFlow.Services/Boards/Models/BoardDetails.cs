
namespace TaskFlow.Services.Boards.Models;

public class BoardDetails
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<BoardColumnItem> Columns { get; set; }
        = new List<BoardColumnItem>();
}
